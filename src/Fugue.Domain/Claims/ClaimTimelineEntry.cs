using Fugue.Domain.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Claims;

/// <summary>
/// One row of what happened to a claim, as the claimant may see it: a transition, with the status before and after, who did it and a
/// claimant-safe note. Append-only (a database trigger refuses UPDATE, DELETE and TRUNCATE). It is its own record, so it is not copied into
/// <c>audit_log</c> as well (the internal field diff of the claim itself is).
/// </summary>
[NoAudit]
public sealed class ClaimTimelineEntry : IAuditable
{
    private ClaimTimelineEntry()
    {
        ActorDisplay = string.Empty;
        Note = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
    }

    public ClaimTimelineEntryId Id { get; private set; }

    public ClaimId ClaimId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Null for the first entry (the claim did not exist yet).</summary>
    public ClaimStatus? FromStatus { get; private set; }

    public ClaimStatus ToStatus { get; private set; }

    public UserId ActorUserId { get; private set; }

    public string ActorDisplay { get; private set; }

    /// <summary>Written for the claimant to read. It never carries the reserve or any internal note.</summary>
    public string Note { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; }

    internal static ClaimTimelineEntry Record(
        ClaimId claimId, DateTimeOffset at, ClaimStatus? from, ClaimStatus to, UserId actor, string actorDisplay, string note) => new()
        {
            Id = ClaimTimelineEntryId.New(),
            ClaimId = claimId,
            OccurredAt = at,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = actor,
            ActorDisplay = actorDisplay,
            Note = note,
        };
}
