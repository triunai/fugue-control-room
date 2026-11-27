using System.Text.Json.Serialization;

namespace Fugue.Application.Features.Claims;

/// <summary>The two ways a claim under review can be decided. Read case-insensitively, so <c>approve</c> and <c>reject</c> are accepted.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimOutcome
{
    Approve,
    Reject,
}
