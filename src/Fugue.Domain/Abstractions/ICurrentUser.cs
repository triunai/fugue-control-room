namespace Fugue.Domain.Abstractions;

/// <summary>The authenticated caller. Tenant comes from the verified token, never from the request.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    bool IsAuthenticated { get; }

    /// <summary>True when the caller's effective permissions (looked up per request) include <paramref name="code"/>.</summary>
    bool HasPermission(string code);
}
