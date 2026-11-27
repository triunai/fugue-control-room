using System.Security.Claims;
using Fugue.Domain.Abstractions;

namespace Fugue.Api.Auth;

/// <summary>Reads <c>sub</c> and <c>tenant_id</c> from the authenticated principal.</summary>
public sealed class JwtCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => IsAuthenticated ? ReadGuid(FugueClaimTypes.Subject, ClaimTypes.NameIdentifier) : null;

    public Guid? TenantId => IsAuthenticated ? ReadGuid(FugueClaimTypes.TenantId) : null;

    public bool HasPermission(string code) =>
        IsAuthenticated && Principal!.HasClaim(FugueClaimTypes.Permission, code);

    private Guid? ReadGuid(params string[] claimTypes)
    {
        foreach (string type in claimTypes)
        {
            string? raw = Principal?.FindFirst(type)?.Value;
            if (Guid.TryParse(raw, out Guid id))
            {
                return id;
            }
        }

        return null;
    }
}
