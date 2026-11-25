using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Fugue.Tests.Identity;

/// <summary>Hand-made JWTs for the rejection gates (wrong issuer, audience, algorithm, expiry, signature).</summary>
internal static class TestTokens
{
    public static string Make(
        string key, Guid sub, DateTimeOffset now,
        string issuer = "fugue", string audience = "fugue-api",
        string algorithm = SecurityAlgorithms.HmacSha256, TimeSpan? lifetime = null, byte[]? keyBytes = null)
    {
        TimeSpan life = lifetime ?? TimeSpan.FromMinutes(15);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = (life < TimeSpan.Zero ? now + life - TimeSpan.FromMinutes(1) : now).UtcDateTime,
            Expires = (now + life).UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Subject = new ClaimsIdentity([new Claim("sub", sub.ToString())]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes ?? Convert.FromBase64String(key)), algorithm),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
