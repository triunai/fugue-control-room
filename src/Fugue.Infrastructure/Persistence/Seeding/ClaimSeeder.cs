using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent development data (decision L): 20 claims across the six markets, both lines and every status, built through the Claim aggregate's own
/// transitions so each timeline is consistent with its status. Written with Dapper in one unit of work. A claim is identified by its number
/// (<c>CLM-&lt;market&gt;-&lt;yyyy&gt;-9000NN</c>; the live sequence starts at 1, so the 9000xx range does not collide until 900,000 claims exist), so a second run finds them all and writes nothing.
/// Dates are relative to the first run. No outbox events are written (like the customer seed): seeding must not fire webhooks.
/// Amounts are bigint minor units; all six currencies are treated as having 2 decimals.
/// </summary>
internal sealed class ClaimSeeder(FugueDbContext db, SqlExecutor sql, IUnitOfWork unitOfWork, IAuditStamp stamp, IAuditTrail audit) : IClaimSeeder
{
    private const int FirstSequence = 900_001;

    private static readonly string InsertClaimSql = SqlScripts.Get("seeding/insert-claim");

    private static readonly string InsertTimelineSql = SqlScripts.Get("seeding/insert-claim-timeline");

    private static readonly string InsertInfoRequestSql = SqlScripts.Get("seeding/insert-claim-info-request");

    private sealed record Spec(
        Market Market, LineOfBusiness Line, string Claimant, double DaysAgo, ClaimStatus Final, string? Officer = null,
        long? Reserve = null, long? Approved = null, bool WithInfo = false);

    // Money in minor units (2 decimals): SGD 1,850.00 = 185000; IDR 15,000,000.00 = 1500000000.
    private static readonly Spec[] Specs =
    [
        new(Market.SG, LineOfBusiness.Motor, DevUsers.Self, 28, ClaimStatus.Settled, DevUsers.Staff, 200_000, 185_000),
        new(Market.SG, LineOfBusiness.Property, DevUsers.Self2, 12, ClaimStatus.Approved, DevUsers.Staff, 900_000, 750_000),
        new(Market.MY, LineOfBusiness.Motor, DevUsers.Self2, 6, ClaimStatus.UnderReview, DevUsers.Staff, 320_000),
        new(Market.MY, LineOfBusiness.Property, DevUsers.Self2, 1, ClaimStatus.Submitted),
        new(Market.TH, LineOfBusiness.Motor, DevUsers.Self, 5, ClaimStatus.InfoRequested, DevUsers.Staff, 4_500_000),
        new(Market.TH, LineOfBusiness.Property, DevUsers.Support, 20, ClaimStatus.Rejected, DevUsers.Staff),
        new(Market.HK, LineOfBusiness.Motor, DevUsers.Self2, 9, ClaimStatus.UnderReview, DevUsers.Manager, 1_200_000),
        new(Market.HK, LineOfBusiness.Property, DevUsers.Self2, 14, ClaimStatus.Approved, DevUsers.Manager, 2_500_000, 2_100_000, WithInfo: true),
        new(Market.ID, LineOfBusiness.Motor, DevUsers.Self2, 2, ClaimStatus.Submitted),
        new(Market.ID, LineOfBusiness.Property, DevUsers.Support, 7, ClaimStatus.UnderReview, DevUsers.Staff, 1_500_000_000),
        new(Market.PH, LineOfBusiness.Motor, DevUsers.Self2, 40, ClaimStatus.Settled, DevUsers.Manager, 6_000_000, 5_400_000),
        new(Market.PH, LineOfBusiness.Property, DevUsers.Self2, 4, ClaimStatus.InfoRequested, DevUsers.Staff, 3_000_000),
        new(Market.SG, LineOfBusiness.Motor, DevUsers.Self, 0.1, ClaimStatus.Submitted),
        new(Market.MY, LineOfBusiness.Motor, DevUsers.Support, 33, ClaimStatus.Rejected, DevUsers.Manager),
        new(Market.TH, LineOfBusiness.Motor, DevUsers.Self2, 8, ClaimStatus.Approved, DevUsers.Staff, 2_000_000, 1_800_000),
        new(Market.SG, LineOfBusiness.Property, DevUsers.Support, 3, ClaimStatus.UnderReview, DevUsers.Staff),
        new(Market.HK, LineOfBusiness.Motor, DevUsers.Self2, 25, ClaimStatus.Settled, DevUsers.Staff, 1_000_000, 950_000, WithInfo: true),
        new(Market.ID, LineOfBusiness.Motor, DevUsers.Self2, 10, ClaimStatus.InfoRequested, DevUsers.Manager),
        new(Market.PH, LineOfBusiness.Motor, DevUsers.Support, 16, ClaimStatus.Submitted),
        new(Market.MY, LineOfBusiness.Property, DevUsers.Self2, 18, ClaimStatus.UnderReview, DevUsers.Manager, 800_000),
    ];

    private static readonly string[] MotorStories =
    [
        "Rear-ended at the traffic lights; the other driver admits fault.",
        "Side-swiped while parked overnight; a note was left on the windscreen.",
        "Hit a pothole and bent a rim and the front suspension.",
        "Reversed into by a delivery van in the car park.",
    ];

    private static readonly string[] PropertyStories =
    [
        "A burst pipe flooded the kitchen and damaged the cabinets and floor.",
        "Storm damage tore roof tiles off and water got into the bedroom ceiling.",
        "A small electrical fire in the study; smoke and soot damage to two rooms.",
        "Break-in through the rear window; a television and a laptop were taken.",
    ];

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        string[] emails = [DevUsers.Self, DevUsers.Self2, DevUsers.Support, DevUsers.Staff, DevUsers.Manager];
        Dictionary<string, (UserId Id, string Name)> users = (await db.Users.AsNoTracking()
                .Where(u => emails.Contains(u.Email) && !u.IsDeleted)
                .Select(u => new { u.Email, u.Id, u.DisplayName })
                .ToListAsync(ct))
            .ToDictionary(u => u.Email, u => (u.Id, u.DisplayName));
        string[] missing = [.. emails.Where(e => !users.ContainsKey(e))];
        if (missing.Length > 0)
        {
            throw new InvalidOperationException("The claims seed needs the development users first; missing: " + string.Join(", ", missing));
        }

        AuditStampValue now = stamp.Capture();
        string[] numbers = [.. Specs.Select((s, i) => NumberFor(s, i, now.Now))];
        HashSet<string> existing = [.. await db.Claims.AsNoTracking().Where(c => numbers.Contains(c.Number)).Select(c => c.Number).ToListAsync(ct)];

        int written = 0;
        await unitOfWork.BeginAsync(ct);
        for (int i = 0; i < Specs.Length; i++)
        {
            if (existing.Contains(numbers[i]))
            {
                continue;
            }

            Claim claim = Build(Specs[i], i, numbers[i], users, now.Now);
            if (await InsertAsync(claim, now.Actor, ct))
            {
                written++;
            }
        }

        await unitOfWork.CommitAsync(ct);
        return written;
    }

    private static string NumberFor(Spec spec, int index, DateTimeOffset now) =>
        ClaimNumber.Format(spec.Market, Markets.LocalToday(spec.Market, now.AddDays(-spec.DaysAgo)).Year, FirstSequence + index);

    private static Claim Build(Spec spec, int index, string number, Dictionary<string, (UserId Id, string Name)> users, DateTimeOffset now)
    {
        DateTimeOffset submitted = now.AddDays(-spec.DaysAgo);
        DateTimeOffset At(double hours) => submitted.AddHours(hours) > now ? now : submitted.AddHours(hours);
        (UserId claimantId, string claimantName) = users[spec.Claimant];
        string[] stories = spec.Line == LineOfBusiness.Motor ? MotorStories : PropertyStories;

        Claim claim = Claim.Submit(
            number, spec.Market, spec.Line, claimantId, claimantName, $"POL-{spec.Market}-{100_200 + (index * 37)}",
            Markets.LocalToday(spec.Market, submitted).AddDays(-1), stories[index % stories.Length],
            spec.Line == LineOfBusiness.Motor ? "Expressway, near the toll plaza" : null, submitted);
        if (spec.Final == ClaimStatus.Submitted)
        {
            return claim;
        }

        (UserId officerId, string officerName) = users[spec.Officer ?? throw new InvalidOperationException($"Seed spec {index} needs an officer.")];
        Ok(claim.Pickup(officerId, officerName, At(3)));
        if (spec.Reserve is { } reserve)
        {
            Ok(claim.SetReserve(reserve));
        }

        if (spec.Final == ClaimStatus.InfoRequested || spec.WithInfo)
        {
            Ok(claim.RequestInfo(
                spec.Line == LineOfBusiness.Motor
                    ? "What is the other driver's registration number, and where is your car now?"
                    : "Which rooms are affected, and is the property safe to stay in tonight?",
                officerId, officerName, At(20)));
            if (spec.Final != ClaimStatus.InfoRequested)
            {
                Ok(claim.ProvideInfo(claim.OpenInfoRequest!.Id, spec.Line == LineOfBusiness.Motor
                        ? "Their registration is SGX 4821 K; the car is at the repair workshop on Kallang Road."
                        : "The kitchen and the hallway; the water is off and we are staying with family tonight.", claimantId, claimantName, At(30)));
            }
        }

        switch (spec.Final)
        {
            case ClaimStatus.Approved:
            case ClaimStatus.Settled:
                Ok(claim.Approve(spec.Approved ?? throw new InvalidOperationException($"Seed spec {index} needs an approved amount."), "Covered under the policy terms.", officerId, officerName, At(72)));
                if (spec.Final == ClaimStatus.Settled)
                {
                    (UserId managerId, string managerName) = users[DevUsers.Manager];
                    Ok(claim.Settle(managerId, managerName, At(96)));
                }

                break;
            case ClaimStatus.Rejected:
                Ok(claim.Reject("The damage pre-dates the policy period.", officerId, officerName, At(72)));
                break;
        }

        return claim;
    }

    private async Task<bool> InsertAsync(Claim claim, string actor, CancellationToken ct)
    {
        DateTimeOffset updated = claim.Timeline.Max(e => e.OccurredAt);
        int inserted = await sql.ExecuteAsync(
            InsertClaimSql,
            new
            {
                claim.Id,
                claim.Number,
                Market = claim.Market.ToString(),
                claim.Currency,
                Line = claim.Line.ToString(),
                Status = claim.Status.ToString(),
                claim.ClaimantUserId,
                claim.AssignedOfficerId,
                claim.AssignedAt,
                claim.PolicyNumber,
                claim.IncidentDate,
                claim.IncidentDescription,
                claim.IncidentLocation,
                claim.SubmittedAt,
                claim.ReserveMinor,
                claim.ApprovedAmountMinor,
                claim.DecisionReason,
                claim.DecidedAt,
                claim.SettledAt,
                CreatedAt = claim.SubmittedAt,
                UpdatedAt = updated,
                Actor = actor,
            },
            ct);
        if (inserted == 0)
        {
            return false;
        }

        foreach (ClaimTimelineEntry e in claim.Timeline)
        {
            await sql.ExecuteAsync(
                InsertTimelineSql,
                new
                {
                    e.Id,
                    e.ClaimId,
                    e.OccurredAt,
                    FromStatus = e.FromStatus?.ToString(),
                    ToStatus = e.ToStatus.ToString(),
                    e.ActorUserId,
                    e.ActorDisplay,
                    e.Note,
                    Actor = actor,
                },
                ct);
        }

        foreach (ClaimInfoRequest r in claim.InfoRequests)
        {
            await sql.ExecuteAsync(
                InsertInfoRequestSql,
                new { r.Id, r.ClaimId, r.Question, r.RequestedByUserId, r.RequestedAt, r.Response, r.RespondedAt, UpdatedAt = r.RespondedAt ?? r.RequestedAt, Actor = actor },
                ct);
        }

        await audit.RecordAsync(
            AuditSnapshot.Created(claim, claim.Id.ToString(), "claims.seed", new Dictionary<string, object?> { ["source"] = "development seed" }), ct);
        return true;
    }

    private static void Ok(Result result)
    {
        if (result.IsFailed)
        {
            throw new InvalidOperationException("The claim seed script made an illegal move: " + result.Errors[0].Message);
        }
    }
}
