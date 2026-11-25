using Fugue.Domain.Ids;

namespace Fugue.Application.Abstractions;

/// <summary>Where a request came from; recorded on sessions and audit rows. Never used for authorization.</summary>
public sealed record ClientInfo(string? Ip, string? UserAgent);

/// <summary>What the per-request permission lookup returns for an active user.</summary>
public sealed record UserAccess(UserId UserId, TenantId? TenantId, IReadOnlySet<string> Permissions);

/// <summary>
/// Effective permissions of a user (docs/plans/s3-identity.md section 2): one indexed query, NO cache (Codex review
/// findings 3-4). Returns null for an unknown, inactive or deleted user, which fails the token.
/// </summary>
public interface IUserAccessReader
{
    Task<UserAccess?> GetAsync(UserId userId, CancellationToken ct);
}

/// <summary>A live (non-deleted) user as login sees it.</summary>
public sealed record LoginUser(UserId Id, string PasswordHash, bool IsActive, bool LocalLoginEnabled);

/// <summary>Reads for the authentication handlers.</summary>
public interface IAuthStore
{
    /// <summary>Looks up a NON-DELETED user by canonical email (<c>EmailKey</c>).</summary>
    Task<LoginUser?> FindLoginUserAsync(string emailKey, CancellationToken ct);
}

/// <summary>Password check. Implementations always spend the same hashing work, even when no hash exists.</summary>
public interface IPasswordVerifier
{
    /// <summary>True when <paramref name="password"/> matches <paramref name="hash"/>. A null hash verifies against a dummy and returns false.</summary>
    bool Verify(string? hash, string password);
}

/// <summary>An issued access token.</summary>
public sealed record IssuedToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Creates signed access tokens. Lives outside Application because it needs the JWT libraries.</summary>
public interface IAccessTokenIssuer
{
    IssuedToken Issue(UserId userId);

    /// <summary>How long a refresh token (and so a session) lives.</summary>
    TimeSpan RefreshLifetime { get; }
}

/// <summary>A refresh token about to be stored: only <see cref="TokenHash"/> ever reaches the database.</summary>
public sealed record NewRefreshToken(UserId UserId, string TokenHash, Guid FamilyId, DateTimeOffset ExpiresAt, ClientInfo Client);

public enum RotationOutcome
{
    Rotated,
    NotFound,

    /// <summary>The token was already used or revoked: the whole family has been revoked.</summary>
    Reused,
    Expired,
    UserInactive,
}

public sealed record RotationResult(RotationOutcome Outcome, UserId? UserId);

/// <summary>Refresh-token persistence. All writes are conditional single statements, so two concurrent refreshes cannot both win.</summary>
public interface IRefreshTokenStore
{
    Task CreateAsync(NewRefreshToken token, CancellationToken ct);

    /// <summary>Atomically revokes <paramref name="oldHash"/> and stores <paramref name="next"/> in the same family, or classifies why not (reuse revokes the family).</summary>
    Task<RotationResult> RotateAsync(string oldHash, NewRefreshToken next, DateTimeOffset now, CancellationToken ct);

    /// <summary>Revokes the whole family of the token with this hash. Unknown hashes are ignored (no oracle).</summary>
    Task RevokeFamilyAsync(string tokenHash, DateTimeOffset now, CancellationToken ct);
}

