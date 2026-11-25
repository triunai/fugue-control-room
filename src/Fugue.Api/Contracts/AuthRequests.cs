namespace Fugue.Api.Contracts;

/// <summary>Anything that carries the email a rate limiter partitions on.</summary>
public interface ILoginIdentifier
{
    string Email { get; }
}

public sealed record LoginRequest(string Email, string Password) : ILoginIdentifier;

public sealed record SetModuleActiveRequest(bool IsActive);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record SetOverrideRequest(bool IsAllowed, string? Reason, DateTimeOffset? ExpiresAt);
