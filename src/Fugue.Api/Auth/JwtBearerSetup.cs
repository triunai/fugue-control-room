using System.Security.Claims;
using Fugue.Api.Http;
using Fugue.Application.Abstractions;
using Fugue.Domain.Ids;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fugue.Api.Auth;

/// <summary>
/// Configures JwtBearer from <see cref="AuthOptions"/>: HS256 only, issuer, audience, signature and lifetime all validated
/// (lifetime against <see cref="TimeProvider"/> so tests and issuance share one clock), claims not remapped. The per-request
/// permission lookup runs in <c>OnTokenValidated</c> (once per request, because the handler caches its result) and can fail
/// the ticket for an inactive or deleted user. 401 and 403 are written as problem+json.
/// </summary>
public sealed class JwtBearerSetup(IOptions<AuthOptions> auth, TimeProvider time) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        AuthOptions o = auth.Value;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false; // symmetric key, no metadata endpoint
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = o.Issuer,
            ValidateAudience = true,
            ValidAudience = o.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(o.KeyBytes()),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(o.ClockSkewSeconds),
            LifetimeValidator = (notBefore, expires, _, p) =>
            {
                DateTime now = time.GetUtcNow().UtcDateTime;
                return expires is not null && expires.Value + p.ClockSkew > now
                    && (notBefore is null || notBefore.Value - p.ClockSkew <= now);
            },
            NameClaimType = "sub",
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = OnTokenValidated,
            OnChallenge = OnChallenge,
            OnForbidden = OnForbidden,
        };
    }

    private static async Task OnTokenValidated(TokenValidatedContext context)
    {
        string? sub = context.Principal?.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out Guid id) || context.Principal?.Identity is not ClaimsIdentity identity)
        {
            context.Fail("Token has no usable subject.");
            return;
        }

        IUserAccessReader reader = context.HttpContext.RequestServices.GetRequiredService<IUserAccessReader>();
        UserAccess? access = await reader.GetAsync(UserId.From(id), context.HttpContext.RequestAborted);
        if (access is null)
        {
            context.Fail("User is unknown, inactive or deleted.");
            return;
        }

        foreach (string permission in access.Permissions)
        {
            identity.AddClaim(new Claim(ClaimsCurrentUser.PermissionClaim, permission));
        }

        if (access.TenantId is { } tenant)
        {
            identity.AddClaim(new Claim("tenant_id", tenant.Value.ToString()));
        }
    }

    private static Task OnChallenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return AuthProblems.WriteAsync(
            context.HttpContext, StatusCodes.Status401Unauthorized, "auth.unauthenticated", "Authentication is required or the token is not valid.");
    }

    private static Task OnForbidden(ForbiddenContext context) =>
        AuthProblems.WriteAsync(
            context.HttpContext, StatusCodes.Status403Forbidden, "auth.forbidden", "You do not have permission to perform this action.");
}
