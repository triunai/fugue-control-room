using System.Threading.RateLimiting;
using Fugue.Api.Contracts;
using Fugue.Api.Http;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Fugue.Api.Auth;

/// <summary><c>RateLimiting:Auth:*</c>. Small windows on purpose: this is the brute-force control (there is no lockout).</summary>
public sealed class AuthRateLimitOptions
{
    public const string Section = "RateLimiting:Auth";

    public int PerIpPermits { get; set; } = 20;

    /// <summary>The strict bucket: attempts per (client IP, canonical email) per <see cref="WindowSeconds"/> (D-035).</summary>
    public int PerEmailPermits { get; set; } = 5;

    public int WindowSeconds { get; set; } = 60;

    /// <summary>Concurrent in-flight attempts per (client IP, canonical email).</summary>
    public int PerEmailConcurrency { get; set; } = 2;

    /// <summary>The loose ceiling: attempts that did not succeed per canonical email, from any IP, per <see cref="EmailCeilingWindowSeconds"/> (D-035).</summary>
    public int EmailCeilingPermits { get; set; } = 50;

    public int EmailCeilingWindowSeconds { get; set; } = 900;
}

public static class AuthRateLimiting
{
    public const string IpPolicy = "auth-ip";

    /// <summary>Registers the built-in rate limiter with the per-client-IP policy and a problem+json 429.</summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthRateLimitOptions>(configuration.GetSection(AuthRateLimitOptions.Section));
        services.AddSingleton<EmailRateLimiter>();
        services.AddScoped<EmailRateLimitFilter>();
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, _) => await WriteRejected(context.HttpContext, context.Lease);
            limiter.AddPolicy(IpPolicy, http =>
            {
                AuthRateLimitOptions o = http.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = o.PerIpPermits, Window = TimeSpan.FromSeconds(o.WindowSeconds), QueueLimit = 0 });
            });
        });
        return services;
    }

    public static Task WriteRejected(HttpContext http, RateLimitLease? lease)
    {
        string retry = lease is not null && lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan after)
            ? Math.Max(1, (int)Math.Ceiling(after.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "60";
        return AuthProblems.WriteAsync(http, StatusCodes.Status429TooManyRequests, "auth.rate_limited", "Too many attempts. Try again later.", retry);
    }
}

/// <summary>
/// D-035. A strict fixed window and a concurrency limit per (client IP, canonical email), plus a loose per-email ceiling
/// across all IPs. The strict bucket is charged on every attempt; the ceiling is only charged by attempts that did not
/// succeed (so a victim's own good logins never count), and is only checked, never charged, by a request the strict bucket
/// already rejected. One IP can therefore throttle only itself against a victim, until the ceiling is reached.
/// </summary>
public sealed class EmailRateLimiter(IOptions<AuthRateLimitOptions> options) : IDisposable
{
    private readonly PartitionedRateLimiter<string> _window = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.Value.PerEmailPermits,
            Window = TimeSpan.FromSeconds(options.Value.WindowSeconds),
            QueueLimit = 0,
        }));

    private readonly PartitionedRateLimiter<string> _concurrency = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetConcurrencyLimiter(key, _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = options.Value.PerEmailConcurrency,
            QueueLimit = 0,
        }));

    private readonly PartitionedRateLimiter<string> _ceiling = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.Value.EmailCeilingPermits,
            Window = TimeSpan.FromSeconds(options.Value.EmailCeilingWindowSeconds),
            QueueLimit = 0,
        }));

    public static string StrictKey(string clientIp, string emailKey) => $"{clientIp}|{emailKey}";

    public (RateLimitLease Window, RateLimitLease Concurrency) Acquire(string strictKey) =>
        (_window.AttemptAcquire(strictKey), _concurrency.AttemptAcquire(strictKey));

    /// <summary>True while the per-email ceiling has headroom (a read: it does not charge).</summary>
    public bool CeilingHasRoom(string emailKey) =>
        _ceiling.GetStatistics(emailKey) is not { CurrentAvailablePermits: <= 0 };

    /// <summary>Charges the ceiling for an attempt that did not succeed.</summary>
    public void ChargeCeiling(string emailKey) => _ceiling.AttemptAcquire(emailKey).Dispose();

    public void Dispose()
    {
        _window.Dispose();
        _concurrency.Dispose();
        _ceiling.Dispose();
    }
}

/// <summary>
/// Runs BEFORE the action (so before the command, the validator and the password hash): the strict (IP, email) bucket,
/// then the per-email ceiling. The per-IP window runs earlier still, in the rate-limiter middleware.
/// </summary>
public sealed class EmailRateLimitFilter(EmailRateLimiter limiter) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string? email = context.ActionArguments.Values.OfType<ILoginIdentifier>().FirstOrDefault()?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            await next();
            return;
        }

        string emailKey = EmailKey.Canonicalize(email);
        string ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        (RateLimitLease window, RateLimitLease concurrency) = limiter.Acquire(EmailRateLimiter.StrictKey(ip, emailKey));
        using (window)
        using (concurrency)
        {
            if (!window.IsAcquired || !concurrency.IsAcquired || !limiter.CeilingHasRoom(emailKey))
            {
                await AuthRateLimiting.WriteRejected(context.HttpContext, !window.IsAcquired ? window : concurrency);
                context.Result = new Microsoft.AspNetCore.Mvc.EmptyResult();
                return;
            }

            ActionExecutedContext executed = await next();
            if (executed.Result is not ObjectResult { StatusCode: null or (>= 200 and < 300) })
            {
                limiter.ChargeCeiling(emailKey);
            }
        }
    }
}
