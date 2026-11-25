using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Overrides;

/// <summary>A per-user allow or deny of one permission. <see cref="Expired"/> is evaluated against the clock at read time.</summary>
public sealed record OverrideDto(UserId UserId, string Permission, bool IsAllowed, string? Reason, DateTimeOffset? ExpiresAt, bool Expired);
