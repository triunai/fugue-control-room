namespace Fugue.Domain.Abstractions;

/// <summary>The authenticated caller. Tenant comes from the verified token, never from the request.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    bool IsAuthenticated { get; }
}
