using System.Reflection;
using FluentResults;
using Fugue.Api.Auth;
using Fugue.Api.Controllers;
using Fugue.Application.Abstractions;
using Fugue.Application.Common;
using Fugue.Application.Features.Claims;
using Fugue.Application.Features.Claims.Commands;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Tests.Claims;

/// <summary>
/// Pairwise cases for the claim actions. The model is <c>tests/pict/claims-actions.pict</c> (role x status x action x assignment x If-Match); the cases
/// are the committed PICT output <c>claims-actions.cases.tsv</c> (every pair of values of any two factors appears at least once). Each case runs the
/// REAL command handler (so <c>ClaimFlow</c>) over the REAL <see cref="Claim"/> aggregate, with an in-memory repository standing in for the database,
/// and the outcome is compared with <see cref="Oracle"/>.
/// <para>
/// The oracle, in the order the guards fire (a lower step wins over a higher one):
/// 1. <b>Policy</b>: the controller action carries <c>[HasPermission(p)]</c>; a role without <c>p</c> never reaches the handler (403). The permission each
///    action needs is read from the controller by reflection, so a changed attribute fails this test. 2. <b>If-Match absent</b>: 428
///    <c>claim.if_match_required</c>. 3. <b>Stale If-Match</b>: 412 <c>claim.version_mismatch</c>, whatever state the claim is in. 4. <b>Assignment</b>
///    (reserve, request info, approve, reject only): the claim is assigned to someone else and the caller lacks <c>CLAIMS_SETTLE</c>: 409
///    <c>claim.not_assigned_to_you</c>. 5. <b>State machine</b>: a move from the wrong status is 409 <c>claim.invalid_transition</c>. Otherwise it succeeds
///    and the claim ends in the target status.
/// </para>
/// Not in the model (covered elsewhere): a claim outside the caller's scope (404; <c>ClaimsWorkflowTests</c>), amount rules and reserve-required
/// (<c>ClaimTests</c>, <c>ClaimNarrativeTests</c>), and persistence/concurrency (<c>ClaimsConcurrencyTests</c>).
/// </para>
/// </summary>
public sealed class ClaimActionPairwiseTests
{
    private static readonly DateTimeOffset T0 = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static readonly Dictionary<string, string[]> RolePermissions = new()
    {
        ["Claimant"] = [Permissions.ClaimsSubmit, Permissions.ClaimsRead, Permissions.ClaimsRespond],
        ["Officer"] = [Permissions.ClaimsRead, Permissions.ClaimsAll, Permissions.ClaimsHandle, Permissions.ClaimsWorkload],
        ["Manager"] =
        [
            Permissions.ClaimsRead, Permissions.ClaimsAll, Permissions.ClaimsHandle, Permissions.ClaimsWorkload, Permissions.ClaimsSettle,
            Permissions.ClaimsExposure,
        ],
    };

    /// <summary>Per action: the controller method, the status it needs, the status it leads to and whether the assigned-officer rule applies.</summary>
    private static readonly Dictionary<string, (string Method, ClaimStatus From, ClaimStatus To, bool AssignedRule)> Actions = new()
    {
        ["Pickup"] = (nameof(ClaimsController.Pickup), ClaimStatus.Submitted, ClaimStatus.UnderReview, false),
        ["Reserve"] = (nameof(ClaimsController.SetReserve), ClaimStatus.UnderReview, ClaimStatus.UnderReview, true),
        ["RequestInfo"] = (nameof(ClaimsController.RequestInfo), ClaimStatus.UnderReview, ClaimStatus.InfoRequested, true),
        ["Approve"] = (nameof(ClaimsController.Decide), ClaimStatus.UnderReview, ClaimStatus.Approved, true),
        ["Reject"] = (nameof(ClaimsController.Decide), ClaimStatus.UnderReview, ClaimStatus.Rejected, true),
        ["Settle"] = (nameof(ClaimsController.Settle), ClaimStatus.Approved, ClaimStatus.Settled, false),
        ["Respond"] = (nameof(ClaimsController.Respond), ClaimStatus.InfoRequested, ClaimStatus.UnderReview, false),
    };

    private static string TsvPath => Path.Combine(AppContext.BaseDirectory, "pict", "claims-actions.cases.tsv");

    public static TheoryData<string, string, string, string, string> Cases()
    {
        var data = new TheoryData<string, string, string, string, string>();
        foreach (string line in File.ReadAllLines(TsvPath).Skip(1).Where(l => l.Length > 0))
        {
            string[] c = line.Split('\t');
            data.Add(c[0], c[1], c[2], c[3], c[4]);
        }

        return data;
    }

    /// <summary>The outcome the guards must produce: <c>null</c> for success, else the error code, or <c>"forbidden"</c> for the policy.</summary>
    public static string? Oracle(string role, ClaimStatus status, string action, string assignment, string ifMatch)
    {
        var rule = Actions[action];
        if (!RolePermissions[role].Contains(RequiredPermission(action)))
        {
            return "forbidden";
        }

        if (ifMatch == "Missing")
        {
            return "claim.if_match_required";
        }

        if (ifMatch == "Stale")
        {
            return "claim.version_mismatch";
        }

        if (rule.AssignedRule && assignment == "Other" && !RolePermissions[role].Contains(Permissions.ClaimsSettle))
        {
            return "claim.not_assigned_to_you";
        }

        return status == rule.From ? null : Claim.InvalidTransitionCode;
    }

    private static string RequiredPermission(string action) =>
        typeof(ClaimsController).GetMethod(Actions[action].Method)!.GetCustomAttribute<HasPermissionAttribute>()!.Permission;

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_generated_case_matches_the_oracle(string role, string statusName, string action, string assignment, string ifMatch)
    {
        ClaimStatus status = Enum.Parse<ClaimStatus>(statusName);
        var caller = new Caller(Guid.NewGuid(), RolePermissions[role]);
        UserId callerId = UserId.From(caller.UserId!.Value);
        UserId owner = role == "Claimant" ? callerId : UserId.New();
        UserId officer = assignment switch { "Me" => callerId, "Other" => UserId.New(), _ => UserId.New() };
        Claim claim = ClaimAt(status, owner, officer);
        var repository = new MemoryRepository(claim);
        int timeline = claim.Timeline.Count;
        long? expected = ifMatch switch { "Current" => claim.RowVersion, "Stale" => claim.RowVersion + 1, _ => null };

        string? outcome = Oracle(role, status, action, assignment, ifMatch);
        if (outcome == "forbidden")
        {
            // The policy answers 403 before any handler runs; the oracle already read it from the controller attribute.
            Assert.DoesNotContain(RequiredPermission(action), RolePermissions[role]);
            return;
        }

        Result<ClaimDto> result = await Run(action, repository, caller, claim, expected);

        if (outcome is null)
        {
            Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
            Assert.Equal(Actions[action].To, claim.Status);
            Assert.Equal(1, repository.Saves);
            Assert.Equal(expected, repository.SavedExpectedVersion);
        }
        else
        {
            FugueError error = Assert.IsType<FugueError>(Assert.Single(result.Errors));
            Assert.Equal(outcome, error.Code);
            Assert.Equal(status, claim.Status); // a refused action changes nothing and saves nothing
            Assert.Equal(timeline, claim.Timeline.Count);
            Assert.Equal(0, repository.Saves);
        }
    }

    [Fact]
    public void The_model_file_and_the_committed_cases_agree()
    {
        string model = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pict", "claims-actions.pict"));
        string[] lines = File.ReadAllLines(TsvPath);
        string[] factors = lines[0].Split('\t');

        Assert.Equal(["Role", "Status", "Action", "Assignment", "IfMatch"], factors);
        foreach (string line in lines.Skip(1).Where(l => l.Length > 0))
        {
            string[] values = line.Split('\t');
            for (int i = 0; i < factors.Length; i++)
            {
                string declared = model.Split('\n').Single(l => l.StartsWith(factors[i] + ":", StringComparison.Ordinal));
                string[] domain = declared[(declared.IndexOf(':', StringComparison.Ordinal) + 1)..].Split(',', StringSplitOptions.TrimEntries);
                Assert.Contains(values[i], domain);
            }
        }

        // A pairwise table over 3 x 6 x 7 x 3 x 3 values needs at least 6 x 7 = 42 cases (the largest pair of factors).
        Assert.True(lines.Skip(1).Count(l => l.Length > 0) >= 42);
    }

    [Fact]
    public void Every_pair_of_values_appears_at_least_once_when_the_constraints_allow_it()
    {
        string[][] rows = File.ReadAllLines(TsvPath).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToArray();
        string[] roles = ["Claimant", "Officer", "Manager"];
        string[] statuses = Enum.GetNames<ClaimStatus>();

        foreach (string role in roles)
        {
            foreach (string status in statuses)
            {
                Assert.Contains(rows, r => r[0] == role && r[1] == status);
            }

            foreach (string action in Actions.Keys)
            {
                Assert.Contains(rows, r => r[0] == role && r[2] == action);
            }
        }

        foreach (string status in statuses)
        {
            foreach (string action in Actions.Keys)
            {
                Assert.Contains(rows, r => r[1] == status && r[2] == action);
            }
        }
    }

    [Fact]
    public void The_generated_table_exercises_success_and_every_kind_of_refusal()
    {
        string?[] outcomes = Cases().Select(c => Oracle((string)c[0], Enum.Parse<ClaimStatus>((string)c[1]), (string)c[2], (string)c[3], (string)c[4])).ToArray();

        Assert.Contains(null, outcomes);
        foreach (string code in new[] { "forbidden", "claim.if_match_required", "claim.version_mismatch", "claim.not_assigned_to_you", Claim.InvalidTransitionCode })
        {
            Assert.Contains(code, outcomes);
        }
    }

    private static Claim ClaimAt(ClaimStatus status, UserId owner, UserId officer)
    {
        Claim claim = Claim.Submit(
            ClaimNumber.Format(Market.SG, 2030, 1), Market.SG, LineOfBusiness.Motor, owner, "Ada Claimant", "POL-1", new DateOnly(2030, 1, 1),
            "Rear-ended.", null, T0);
        if (status == ClaimStatus.Submitted)
        {
            return claim;
        }

        Assert.True(claim.Pickup(officer, "Officer", T0).IsSuccess);
        Assert.True(claim.SetReserve(1000).IsSuccess);
        switch (status)
        {
            case ClaimStatus.InfoRequested:
                Assert.True(claim.RequestInfo("Photos?", officer, "Officer", T0).IsSuccess);
                break;
            case ClaimStatus.Approved or ClaimStatus.Settled:
                Assert.True(claim.Approve(800, "Covered.", officer, "Officer", T0).IsSuccess);
                if (status == ClaimStatus.Settled)
                {
                    Assert.True(claim.Settle(officer, "Officer", T0).IsSuccess);
                }

                break;
            case ClaimStatus.Rejected:
                Assert.True(claim.Reject("Not covered.", officer, "Officer", T0).IsSuccess);
                break;
        }

        return claim;
    }

    private static async Task<Result<ClaimDto>> Run(string action, MemoryRepository repository, Caller caller, Claim claim, long? expected)
    {
        var stamp = new FixedStamp();
        var actors = new FixedActors();
        CancellationToken ct = CancellationToken.None;
        ClaimId id = claim.Id;
        return action switch
        {
            "Pickup" => await new PickupClaimHandler(repository, caller, stamp, actors).Handle(new PickupClaimCommand(id, expected), ct),
            "Reserve" => await new ReserveClaimHandler(repository, caller).Handle(new ReserveClaimCommand(id, 2000, expected), ct),
            "RequestInfo" => await new RequestClaimInfoHandler(repository, caller, stamp, actors).Handle(new RequestClaimInfoCommand(id, "Anything else?", expected), ct),
            "Approve" => await new DecideClaimHandler(repository, caller, stamp, actors)
                .Handle(new DecideClaimCommand(id, ClaimOutcome.Approve, 500, "Covered.", expected), ct),
            "Reject" => await new DecideClaimHandler(repository, caller, stamp, actors)
                .Handle(new DecideClaimCommand(id, ClaimOutcome.Reject, null, "Not covered.", expected), ct),
            "Settle" => await new SettleClaimHandler(repository, caller, stamp, actors).Handle(new SettleClaimCommand(id, expected), ct),
            "Respond" => await new RespondToClaimInfoHandler(repository, caller, stamp, actors)
                .Handle(new RespondToClaimInfoCommand(id, claim.OpenInfoRequest?.Id ?? ClaimInfoRequestId.New(), "Attached.", expected), ct),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private sealed class Caller(Guid userId, IEnumerable<string> permissions) : ICurrentUser
    {
        private readonly HashSet<string> _permissions = [.. permissions];

        public Guid? UserId => userId;

        public Guid? TenantId => null;

        public bool IsAuthenticated => true;

        public bool HasPermission(string code) => _permissions.Contains(code);
    }

    private sealed class FixedStamp : IAuditStamp
    {
        public AuditStampValue Capture() => new(T0.AddHours(1), "test");
    }

    private sealed class FixedActors : IActorDirectory
    {
        public Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct) => Task.FromResult<string?>("Test User");
    }

    /// <summary>One claim, found only inside the caller's scope exactly as the real repository does; counts the saves.</summary>
    private sealed class MemoryRepository(Claim claim) : IClaimRepository
    {
        public int Saves { get; private set; }

        public long? SavedExpectedVersion { get; private set; }

        public Task<Claim?> FindForUpdateAsync(ClaimId id, ClaimScope scope, CancellationToken ct) =>
            Task.FromResult<Claim?>(id == claim.Id && (scope.All || scope.ClaimantId == claim.ClaimantUserId) ? claim : null);

        public Task<Result> SaveAsync(Claim saved, long expectedVersion, CancellationToken ct)
        {
            Saves++;
            SavedExpectedVersion = expectedVersion;
            return Task.FromResult(Result.Ok());
        }

        public Task<ClaimDto?> GetAsync(ClaimId id, ClaimScope scope, CancellationToken ct) =>
            Task.FromResult<ClaimDto?>(new ClaimDto(
                claim.Id, claim.Number, claim.Market, claim.Currency, claim.Line, claim.Status, claim.PolicyNumber, claim.IncidentDate,
                claim.IncidentDescription, claim.IncidentLocation, claim.SubmittedAt, claim.ApprovedAmountMinor, claim.DecisionReason, claim.DecidedAt,
                claim.SettledAt, claim.UpdatedAt, claim.RowVersion, null));

        public Task<long> NextNumberAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<Result> AddAsync(Claim added, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<ClaimDto>> ListAsync(ClaimFilter filter, KeysetCursor? after, int take, ClaimScope scope, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ClaimTimelineEntryDto>?> GetTimelineAsync(ClaimId id, ClaimScope scope, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
