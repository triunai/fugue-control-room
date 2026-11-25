using Fugue.Domain.Results;

namespace Fugue.Application.Features.Auth;

/// <summary>Stable error codes of the Auth slice.</summary>
public static class AuthErrors
{
    /// <summary>The ONE failure for unknown email, wrong password, inactive, deleted or local login disabled.</summary>
    public static FugueError InvalidCredentials() =>
        FugueErrors.Unauthorized("auth.invalid_credentials", "The email or password is incorrect.");

    /// <summary>The ONE failure for an unknown, reused, revoked, expired refresh token or an inactive user.</summary>
    public static FugueError InvalidRefreshToken() =>
        FugueErrors.Unauthorized("auth.invalid_refresh_token", "The refresh token is not valid. Sign in again.");
}
