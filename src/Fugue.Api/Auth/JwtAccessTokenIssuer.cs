using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fugue.Application.Abstractions;
using Fugue.Domain.Ids;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fugue.Api.Auth;

/// <summary>Mints HS256 access tokens carrying only identity claims; permissions are looked up per request, never embedded.</summary>
public sealed class JwtAccessTokenIssuer(IOptions<AuthOptions> options, TimeProvider time) : IAccessTokenIssuer
{
    public TimeSpan RefreshLifetime => TimeSpan.FromDays(options.Value.RefreshTokenDays);

    public IssuedToken Issue(UserId userId)
    {
        AuthOptions o = options.Value;
        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset expires = now.AddMinutes(o.AccessTokenMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(o.KeyBytes()), SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false };
        return new IssuedToken(handler.WriteToken(handler.CreateToken(descriptor)), expires);
    }
}
