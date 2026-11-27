using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Claims;

/// <summary>A question an officer put to the claimant, and the answer. At most one is open (unanswered) per claim.</summary>
public sealed class ClaimInfoRequest : IAuditable
{
    private ClaimInfoRequest()
    {
        Question = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
    }

    public ClaimInfoRequestId Id { get; private set; }

    public ClaimId ClaimId { get; private set; }

    public string Question { get; private set; }

    public UserId RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public string? Response { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public bool IsOpen => RespondedAt is null;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; }

    internal static ClaimInfoRequest Open(ClaimId claimId, string question, UserId requestedBy, DateTimeOffset at) => new()
    {
        Id = ClaimInfoRequestId.New(),
        ClaimId = claimId,
        Question = question,
        RequestedByUserId = requestedBy,
        RequestedAt = at,
    };

    internal void Answer(string response, DateTimeOffset at)
    {
        Response = response;
        RespondedAt = at;
    }
}
