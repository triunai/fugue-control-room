using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Common;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Fugue.Infrastructure.Persistence.Claims;

/// <summary>
/// ADR-006: a Dapper insert (claim, first timeline row, audit row, outbox event in one transaction), EF reads (AsNoTracking projections, keyset paging) and
/// EF tracked updates guarded by the xmin concurrency token. New timeline rows, info requests and outbox events written by an update ride the same SaveChanges.
/// </summary>
internal sealed class ClaimRepository(FugueDbContext db, SqlExecutor sql, IAuditStamp stamp, IAuditTrail audit, IOutbox outbox) : IClaimRepository
{
    private const string UniqueViolation = "23505";

    private static readonly string InsertClaimSql = SqlScripts.Get("claims/insert");

    private static readonly string InsertTimelineSql = SqlScripts.Get("claims/insert-timeline");

    private static readonly string NextNumberSql = SqlScripts.Get("claims/next-number");

    public Task<long> NextNumberAsync(CancellationToken ct) => sql.ExecuteScalarAsync<long>(NextNumberSql, null, ct);

    public async Task<Result> AddAsync(Claim claim, CancellationToken ct)
    {
        AuditStampValue now = stamp.Capture();
        claim.CreatedAt = claim.UpdatedAt = now.Now;
        claim.CreatedBy = claim.UpdatedBy = now.Actor;

        // The claim, its first timeline row, its audit row and its event are one transaction: join the caller's unit of work, or open a short one.
        IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await sql.ExecuteAsync(
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
                    now.Now,
                    now.Actor,
                },
                ct);
            foreach (ClaimTimelineEntry entry in claim.Timeline)
            {
                await sql.ExecuteAsync(
                    InsertTimelineSql,
                    new
                    {
                        entry.Id,
                        entry.ClaimId,
                        entry.OccurredAt,
                        FromStatus = entry.FromStatus?.ToString(),
                        ToStatus = entry.ToStatus.ToString(),
                        entry.ActorUserId,
                        entry.ActorDisplay,
                        entry.Note,
                        now.Now,
                        now.Actor,
                    },
                    ct);
            }

            await audit.RecordAsync(AuditSnapshot.Created(claim, claim.Id.ToString(), "claims.submit"), ct);
            await outbox.EnqueueAsync(claim, ct); // same transaction: the event exists if and only if the insert commits (ADR-011)
            if (own is not null)
            {
                await own.CommitAsync(ct);
            }

            return Result.Ok();
        }
        finally
        {
            if (own is not null)
            {
                await own.DisposeAsync(); // not committed: rolls back
            }
        }
    }

    public async Task<ClaimDto?> GetAsync(ClaimId id, ClaimScope scope, CancellationToken ct)
    {
        Row? row = await Project(Scoped(db.Claims.AsNoTracking(), scope).Where(c => c.Id == id)).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        ClaimInfoRequestDto? open = await db.ClaimInfoRequests.AsNoTracking()
            .Where(r => r.ClaimId == id && r.RespondedAt == null)
            .Select(r => new ClaimInfoRequestDto(r.Id, r.Question, r.RequestedAt))
            .FirstOrDefaultAsync(ct);
        return row.ToDto(scope, open);
    }

    public async Task<IReadOnlyList<ClaimDto>> ListAsync(ClaimFilter filter, KeysetCursor? after, int take, ClaimScope scope, CancellationToken ct)
    {
        bool ascending = scope.All; // staff work the queue oldest first; a claimant sees their own newest first

        IQueryable<Claim> query = after is { } cursor
            // Row-value comparison is not expressible in LINQ; the rest of the query composes on top of this.
            ? ascending
                ? db.Claims.FromSqlRaw(SqlScripts.Get("claims/list-after-asc"), new NpgsqlParameter("createdAt", cursor.CreatedAt), new NpgsqlParameter("id", cursor.Id))
                : db.Claims.FromSqlRaw(SqlScripts.Get("claims/list-after-desc"), new NpgsqlParameter("createdAt", cursor.CreatedAt), new NpgsqlParameter("id", cursor.Id))
            : db.Claims;

        query = Scoped(query.AsNoTracking(), scope);
        if (filter.Statuses.Count > 0)
        {
            ClaimStatus[] statuses = [.. filter.Statuses];
            query = query.Where(c => statuses.Contains(c.Status));
        }

        query = filter.Assigned switch
        {
            ClaimAssignment.None => query.Where(c => c.AssignedOfficerId == null),
            ClaimAssignment.Me when scope.ClaimantId is { } me => query.Where(c => c.AssignedOfficerId == me),
            ClaimAssignment.Me => query.Where(_ => false),
            _ => query,
        };

        if (filter.Market is { } market)
        {
            query = query.Where(c => c.Market == market);
        }

        if (filter.Line is { } line)
        {
            query = query.Where(c => c.Line == line);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            string pattern = $"%{EscapeLike(filter.Search)}%";
            query = query.Where(c => EF.Functions.ILike(c.Number, pattern, "\\") || EF.Functions.ILike(c.PolicyNumber, pattern, "\\"));
        }

        query = ascending
            ? query.OrderBy(c => c.SubmittedAt).ThenBy(c => c.Id)
            : query.OrderByDescending(c => c.SubmittedAt).ThenByDescending(c => c.Id);

        List<Row> rows = await Project(query.Take(take)).ToListAsync(ct);
        return rows.Select(r => r.ToDto(scope, null)).ToList();
    }

    public async Task<IReadOnlyList<ClaimTimelineEntryDto>?> GetTimelineAsync(ClaimId id, ClaimScope scope, CancellationToken ct)
    {
        if (!await Scoped(db.Claims.AsNoTracking(), scope).AnyAsync(c => c.Id == id, ct))
        {
            return null;
        }

        var rows = await db.ClaimTimeline.AsNoTracking()
            .Where(e => e.ClaimId == id)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.OccurredAt, e.FromStatus, e.ToStatus, e.ActorUserId, e.ActorDisplay, e.Note })
            .ToListAsync(ct);

        // A claimant reads their own name on their own actions; everything staff did reads as the team (no officer names leave the building).
        return rows.Select(e => new ClaimTimelineEntryDto(
            e.Id, e.OccurredAt, e.FromStatus, e.ToStatus,
            scope.All || e.ActorUserId == scope.ClaimantId ? e.ActorDisplay : "Claims team",
            e.Note)).ToList();
    }

    public Task<Claim?> FindForUpdateAsync(ClaimId id, ClaimScope scope, CancellationToken ct) =>
        Scoped(db.Claims.Include(c => c.InfoRequests.Where(r => r.RespondedAt == null)), scope).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Result> SaveAsync(Claim claim, long expectedVersion, CancellationToken ct)
    {
        // Compare the loaded version with If-Match, and pin it as the EF original value so a write that raced in between the load
        // and the save also fails (the UPDATE carries WHERE xmin = @expected).
        if (expectedVersion != claim.RowVersion)
        {
            return Result.Fail(ClaimErrors.VersionMismatch());
        }

        db.Entry(claim).Property(c => c.RowVersion).OriginalValue = (uint)expectedVersion;
        db.ChangeTracker.DetectChanges();
        if (db.Entry(claim).State == EntityState.Unchanged)
        {
            // A command that leaves every column as loaded (a reserve set to its current value) would issue no UPDATE, so the xmin check would never
            // run and a racing write would be answered with a stale success. Forcing the write keeps the precondition honest and bumps the version.
            db.Entry(claim).Property(c => c.UpdatedAt).IsModified = true;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail(ClaimErrors.VersionMismatch());
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // Two officers asking at once: the partial unique index lets only one open question through.
            return Result.Fail(Fugue.Domain.Results.FugueErrors.Conflict(Claim.InvalidTransitionCode, "The claim already has an open information request."));
        }

        return Result.Ok();
    }

    /// <summary>The visibility filter, applied to every read and find-for-update (so keyset pages are filtered too). A caller with neither <c>CLAIMS_ALL</c> nor an id matches nothing.</summary>
    private static IQueryable<Claim> Scoped(IQueryable<Claim> query, ClaimScope scope) =>
        scope.All ? query
        : scope.ClaimantId is { } owner ? query.Where(c => c.ClaimantUserId == owner)
        : query.Where(_ => false);

    private IQueryable<Row> Project(IQueryable<Claim> claims) =>
        from c in claims
        join claimant in db.Users on c.ClaimantUserId equals claimant.Id
        join officer in db.Users on c.AssignedOfficerId equals (UserId?)officer.Id into officers
        from o in officers.DefaultIfEmpty()
        select new Row(
            c.Id, c.Number, c.Market, c.Currency, c.Line, c.Status, c.PolicyNumber, c.IncidentDate, c.IncidentDescription, c.IncidentLocation,
            c.SubmittedAt, c.ApprovedAmountMinor, c.DecisionReason, c.DecidedAt, c.SettledAt, c.UpdatedAt, c.RowVersion,
            c.ClaimantUserId, claimant.DisplayName, c.AssignedOfficerId, o.DisplayName, c.AssignedAt, c.ReserveMinor);

    // xid has no cast to bigint in SQL, so the projection keeps the raw uint and widens it in memory.
    private sealed record Row(
        ClaimId Id, string Number, Market Market, string Currency, LineOfBusiness Line, ClaimStatus Status, string PolicyNumber, DateOnly IncidentDate,
        string IncidentDescription, string? IncidentLocation, DateTimeOffset SubmittedAt, long? ApprovedAmountMinor, string? DecisionReason,
        DateTimeOffset? DecidedAt, DateTimeOffset? SettledAt, DateTimeOffset UpdatedAt, uint RowVersion,
        UserId ClaimantUserId, string ClaimantName, UserId? AssignedOfficerId, string? AssignedOfficerName, DateTimeOffset? AssignedAt, long? ReserveMinor)
    {
        /// <summary>Staff (CLAIMS_ALL) get the internal group; a claimant gets nulls there, and never the reserve.</summary>
        public ClaimDto ToDto(ClaimScope scope, ClaimInfoRequestDto? open) => scope.All
            ? new ClaimDto(
                Id, Number, Market, Currency, Line, Status, PolicyNumber, IncidentDate, IncidentDescription, IncidentLocation, SubmittedAt,
                ApprovedAmountMinor, DecisionReason, DecidedAt, SettledAt, UpdatedAt, RowVersion, open,
                ClaimantUserId, ClaimantName, AssignedOfficerId, AssignedOfficerName, AssignedAt, ReserveMinor)
            : new ClaimDto(
                Id, Number, Market, Currency, Line, Status, PolicyNumber, IncidentDate, IncidentDescription, IncidentLocation, SubmittedAt,
                ApprovedAmountMinor, DecisionReason, DecidedAt, SettledAt, UpdatedAt, RowVersion, open);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
