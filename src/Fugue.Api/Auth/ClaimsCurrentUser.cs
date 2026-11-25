using System.Security.Claims;
using Fugue.Domain.Abstractions;

namespace Fugue.Api.Auth;

/// <summary>Reads <c>sub</c> and <c>tenant_id</c> from the authenticated principal.</summary>
public sealed class ClaimsCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => IsAuthenticated ? ReadGuid("sub", ClaimTypes.NameIdentifier) : null;

    public Guid? TenantId => IsAuthenticated ? ReadGuid("tenant_id") : null;

    /// <summary>The claim type under which <c>OnTokenValidated</c> adds each effective permission code (in memory only).</summary>
    public const string PermissionClaim = "perm";

    public bool HasPermission(string code) =>
        IsAuthenticated && Principal!.HasClaim(PermissionClaim, code);

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
