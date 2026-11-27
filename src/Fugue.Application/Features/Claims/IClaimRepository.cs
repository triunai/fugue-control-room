using FluentResults;
using Fugue.Application.Common;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Claims;

/// <summary>Which claims a list returns, besides the caller's scope.</summary>
public sealed record ClaimFilter(
    IReadOnlyList<ClaimStatus> Statuses, ClaimAssignment Assigned, Market? Market, LineOfBusiness? Line, string? Search);

/// <summary><c>me</c>: assigned to the caller; <c>none</c>: unassigned; <c>any</c>: no filter.</summary>
public enum ClaimAssignment
{
    Any,
    Me,
    None,
}

/// <summary>Persistence contract for the Claims slice (ADR-006): inserts use Dapper, reads and concurrency-checked updates use EF Core.</summary>
public interface IClaimRepository
{
    /// <summary>The next value of the claim-number sequence (atomic; two callers never get the same value).</summary>
    Task<long> NextNumberAsync(CancellationToken ct);

    /// <summary>
    /// Insert (Dapper): the claim, its first timeline row, the audit row and the outbox event commit together or not at all.
    /// </summary>
    Task<Result> AddAsync(Claim claim, CancellationToken ct);

    /// <summary>Read (EF, no tracking), with the open question. A claim outside <paramref name="scope"/> is indistinguishable from an absent one.</summary>
    Task<ClaimDto?> GetAsync(ClaimId id, ClaimScope scope, CancellationToken ct);

    /// <summary>
    /// Keyset page ordered by <c>(submitted_at, id)</c>: ascending (oldest first, the work queue) for a staff caller, descending (newest first)
    /// for a claimant. Returns up to <paramref name="take"/> rows after <paramref name="after"/>; the caller asks for one extra to learn whether another page exists.
    /// </summary>
    Task<IReadOnlyList<ClaimDto>> ListAsync(ClaimFilter filter, KeysetCursor? after, int take, ClaimScope scope, CancellationToken ct);

    /// <summary>The claim's history, oldest first; null when the claim is outside <paramref name="scope"/>.</summary>
    Task<IReadOnlyList<ClaimTimelineEntryDto>?> GetTimelineAsync(ClaimId id, ClaimScope scope, CancellationToken ct);

    /// <summary>Loads a tracked aggregate (with its open info request) for a following <see cref="SaveAsync"/>.</summary>
    Task<Claim?> FindForUpdateAsync(ClaimId id, ClaimScope scope, CancellationToken ct);

    /// <summary>
    /// Saves a tracked change (EF) together with its new timeline rows, info requests and outbox events. A stored version that differs from
    /// <paramref name="expectedVersion"/> is <c>claim.version_mismatch</c> (PreconditionFailed).
    /// </summary>
    Task<Result> SaveAsync(Claim claim, long expectedVersion, CancellationToken ct);
}
