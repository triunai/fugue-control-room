using System.Threading.RateLimiting;
using Fugue.Api.Contracts;
using Fugue.Api.Http;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Fugue.Api.Auth;

/// <summary><c>RateLimiting:Auth:*</c>. Small windows on purpose: this is the brute-force control (there is no lockout).</summary>
public sealed class AuthRateLimitOptions
{
    public const string Section = "RateLimiting:Auth";

    public int PerIpPermits { get; set; } = 20;

    public int PerEmailPermits { get; set; } = 5;

    public int WindowSeconds { get; set; } = 60;

    public int PerEmailConcurrency { get; set; } = 2;
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

/// <summary>Per canonical-email fixed window plus a concurrency limit; one instance for the process.</summary>
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

    public (RateLimitLease Window, RateLimitLease Concurrency) Acquire(string emailKey) =>
        (_window.AttemptAcquire(emailKey), _concurrency.AttemptAcquire(emailKey));

    public void Dispose()
    {
        _window.Dispose();
        _concurrency.Dispose();
    }
}

/// <summary>
/// Runs BEFORE the action (so before the command, the validator and the password hash): partitions by canonical email.
/// The per-IP window runs earlier still, in the rate-limiter middleware.
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

        (RateLimitLease window, RateLimitLease concurrency) = limiter.Acquire(EmailKey.Canonicalize(email));
        using (window)
        using (concurrency)
        {
            if (!window.IsAcquired || !concurrency.IsAcquired)
            {
                await AuthRateLimiting.WriteRejected(context.HttpContext, window.IsAcquired ? concurrency : window);
                context.Result = new Microsoft.AspNetCore.Mvc.EmptyResult();
                return;
            }

            await next();
        }
    }
}
