using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Overrides.Models;

/// <summary>
/// A per-user allow or deny of one permission. <see cref="Expired"/> is evaluated against the clock at read time.
/// <see cref="Version"/> is the ETag a later PUT or DELETE sends back in <c>If-Match</c>.
/// </summary>
public sealed record OverrideDto(UserId UserId, string Permission, bool IsAllowed, string? Reason, DateTimeOffset? ExpiresAt, bool Expired, long Version) : IVersioned;
