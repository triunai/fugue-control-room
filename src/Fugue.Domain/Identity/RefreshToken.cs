using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Identity;

/// <summary>A refresh token session (UAM <c>user_sessions</c>).</summary>
public sealed class RefreshToken : IAuditable
{
    public RefreshTokenId Id { get; set; }

    public UserId UserId { get; set; }

    /// <summary>SHA-256 of the opaque token (hex). The token itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>All tokens descended from one login share a family; reuse of a revoked one revokes the family.</summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public RefreshTokenId? ReplacedById { get; set; }

    public string? CreatedIp { get; set; }

    public string? CreatedUserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
