using System.Threading.RateLimiting;
using Fugue.Infrastructure.Audit;
using Microsoft.Extensions.Options;

namespace Fugue.Api.Auth;

/// <summary>
/// The flood bound for <c>access_audit</c> rows of 401 challenges: a fixed one-minute window per client IP in memory
/// (<c>Audit:UnauthenticatedRowsPerIpPerMinute</c>; 0 records none). It only decides whether a row is WRITTEN; the request is answered
/// 401 either way. The framework's partitioned limiter drops idle IP partitions, so memory stays bounded under a spray of addresses.
/// </summary>
public sealed class UnauthenticatedAuditLimiter : IDisposable
{
    private readonly int _perMinute;
    private readonly PartitionedRateLimiter<string> _window;

    public UnauthenticatedAuditLimiter(IOptions<AuditOptions> options)
    {
        _perMinute = options.Value.UnauthenticatedRowsPerIpPerMinute;
        _window = PartitionedRateLimiter.Create<string, string>(ip =>
            RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, _perMinute),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }

    /// <summary>True when a row may be written for this client IP now; false when it is over the cap (or the cap is 0).</summary>
    public bool TryAcquire(string? clientIp)
    {
        if (_perMinute <= 0)
        {
            return false;
        }

        using RateLimitLease lease = _window.AttemptAcquire(clientIp ?? "unknown");
        return lease.IsAcquired;
    }

    public void Dispose() => _window.Dispose();
}
