using Fugue.Domain.Results;

namespace Fugue.Application.Features.Auth;

/// <summary>Stable error codes of the Auth slice.</summary>
public static class AuthErrors
{
    /// <summary>The ONE failure for unknown email, wrong password, inactive, deleted or local login disabled.</summary>
    public static FugueError InvalidCredentials() =>
        FugueErrors.Unauthorized("auth.invalid_credentials", "The email or password is incorrect.");
}
