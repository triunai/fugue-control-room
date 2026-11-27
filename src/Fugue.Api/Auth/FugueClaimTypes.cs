using System.IdentityModel.Tokens.Jwt;

namespace Fugue.Api.Auth;

/// <summary>The claim type names the API reads and writes, in one place.</summary>
public static class FugueClaimTypes
{
    /// <summary>The standard JWT subject claim: the user id.</summary>
    public const string Subject = JwtRegisteredClaimNames.Sub;

    /// <summary>The tenant id; set only when tenancy is on.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>One effective permission code; added in memory in <c>OnTokenValidated</c> and never put in the token.</summary>
    public const string Permission = "perm";
}
