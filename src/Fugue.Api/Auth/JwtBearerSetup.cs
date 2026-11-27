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
/// the ticket for an inactive or deleted user. 401 and 403 are written as problem+json; both leave an <c>access_audit</c> row
/// (the 401 one is bounded per client IP, <see cref="UnauthenticatedAuditLimiter"/>).
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

        // Defence in depth: the issuer never puts permissions in a token, but a token that somehow carries `perm` claims must not keep them.
        foreach (Claim stale in identity.FindAll(ClaimsCurrentUser.PermissionClaim).ToList())
        {
            identity.RemoveClaim(stale);
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

    private static async Task OnChallenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        await RecordUnauthenticatedAsync(context);
        context.Response.Headers.WWWAuthenticate = "Bearer";
        await AuthProblems.WriteAsync(
            context.HttpContext, StatusCodes.Status401Unauthorized, "auth.unauthenticated", "Authentication is required or the token is not valid.");
    }

    /// <summary>
    /// Writes the <c>unauthenticated</c>/<c>denied</c> access_audit row of a 401, bounded per client IP. The client is told nothing about the
    /// reason (the response is identical for all three); the token itself is never read into the row.
    /// </summary>
    private static async Task RecordUnauthenticatedAsync(JwtBearerChallengeContext context)
    {
        HttpContext http = context.HttpContext;
        try
        {
            ClientInfo client = AuthProblems.ClientOf(http);
            if (!http.RequestServices.GetRequiredService<UnauthenticatedAuditLimiter>().TryAcquire(client.Ip))
            {
                AuditMetrics.RecordWriteFailure(AuditMetrics.AccessAuditTable, AuditMetrics.SuppressedReason);
                return;
            }

            string reason = ReasonFor(context.AuthenticateFailure, http.Request.Headers.ContainsKey("Authorization"), http.RequestServices.GetRequiredService<TimeProvider>());
            await http.RequestServices.GetRequiredService<IAccessAuditWriter>().WriteAsync(
                new AccessAuditEntry("unauthenticated", "denied", reason, null, null, client), http.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Auditing is best effort: a 401 must stay a 401 even when the audit services cannot be built (no database, a broken container).
            AuditMetrics.RecordWriteFailure(AuditMetrics.AccessAuditTable, ex.GetType().Name);
            ChallengeAuditFailed(http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(JwtBearerSetup).FullName!), ex);
        }
    }

    private static readonly Action<ILogger, Exception?> ChallengeAuditLog =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1), "Could not record the unauthenticated access_audit row for a 401");

    private static void ChallengeAuditFailed(ILogger? logger, Exception ex)
    {
        if (logger is not null)
        {
            ChallengeAuditLog(logger, ex);
        }
    }

    /// <summary>missing_token (no credentials sent), expired (a lifetime failure past its expiry), invalid_token (everything else).</summary>
    private static string ReasonFor(Exception? failure, bool authorizationSent, TimeProvider time) => failure switch
    {
        null => authorizationSent ? "invalid_token" : "missing_token",
        AggregateException aggregate => aggregate.InnerExceptions.Any(e => ReasonFor(e, authorizationSent, time) == "expired") ? "expired" : "invalid_token",
        SecurityTokenExpiredException => "expired",
        SecurityTokenInvalidLifetimeException { Expires: { } expires } when expires.ToUniversalTime() <= time.GetUtcNow().UtcDateTime => "expired",
        _ => "invalid_token",
    };

    private static async Task OnForbidden(ForbiddenContext context)
    {
        HttpContext http = context.HttpContext;
        // The client is told nothing about WHICH permission; the audit row records it (internal) for operators.
        string? permissions = string.Join(',', http.GetEndpoint()?.Metadata.GetOrderedMetadata<HasPermissionAttribute>().Select(a => a.Permission) ?? []);
        _ = Guid.TryParse(http.User.FindFirstValue("sub"), out Guid sub);
        await http.RequestServices.GetRequiredService<IAccessAuditWriter>().WriteAsync(
            new AccessAuditEntry("forbidden", "denied", permissions.Length > 0 ? permissions : null, sub == Guid.Empty ? null : UserId.From(sub), null, AuthProblems.ClientOf(http)),
            http.RequestAborted);
        await AuthProblems.WriteAsync(http, StatusCodes.Status403Forbidden, "auth.forbidden", "You do not have permission to perform this action.");
    }
}
