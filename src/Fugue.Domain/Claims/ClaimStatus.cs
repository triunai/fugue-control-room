using System.Text.Json.Serialization;

namespace Fugue.Domain.Claims;

/// <summary>Where a claim is in its life. Rejected and Settled are terminal.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimStatus
{
    Submitted,
    UnderReview,
    InfoRequested,
    Approved,
    Rejected,
    Settled,
}
