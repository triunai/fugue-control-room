using Fugue.Infrastructure.Audit;
using Microsoft.Extensions.Options;

namespace Fugue.Api.Auth;

/// <summary>
/// The flood bound for <c>access_audit</c> rows of 401 challenges: at most <c>Audit:UnauthenticatedRowsPerIpPerMinute</c> rows per bucket
/// per fixed one-minute window (0 records none). It only decides whether a row is WRITTEN; the request is answered 401 either way.
/// Memory is O(<c>Audit:UnauthenticatedBuckets</c>) whatever the address spray: a client IP maps by a per-process-seeded hash to one of a
/// fixed array of buckets, each a packed (minute index, count) word updated by compare-and-swap. Colliding addresses share a bucket, which
/// can only suppress more rows, never write more. The key is the connection's remote address, never a forwarding header.
/// </summary>
public sealed class UnauthenticatedAuditLimiter
{
    private readonly int _perMinute;
    private readonly TimeProvider _time;
    private readonly long[] _buckets; // (minute index << 32) | count

    public UnauthenticatedAuditLimiter(IOptions<AuditOptions> options, TimeProvider time)
    {
        _perMinute = options.Value.UnauthenticatedRowsPerIpPerMinute;
        _time = time;
        _buckets = new long[Math.Max(1, options.Value.UnauthenticatedBuckets)];
    }

    /// <summary>The fixed number of buckets; it never grows.</summary>
    public int BucketCount => _buckets.Length;

    /// <summary>The bucket a client IP falls in (a null address shares the <c>unknown</c> bucket).</summary>
    public int BucketFor(string? clientIp) => (int)((uint)(clientIp ?? "unknown").GetHashCode() % (uint)_buckets.Length);

    /// <summary>True when a row may be written for this client IP now; false when its bucket is over the cap (or the cap is 0).</summary>
    public bool TryAcquire(string? clientIp)
    {
        if (_perMinute <= 0)
        {
            return false;
        }

        long minute = _time.GetUtcNow().ToUnixTimeSeconds() / 60;
        ref long slot = ref _buckets[BucketFor(clientIp)];
        while (true)
        {
            long seen = Volatile.Read(ref slot);
            long count = (seen >> 32) == minute ? seen & uint.MaxValue : 0;
            if (count >= _perMinute)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref slot, (minute << 32) | (count + 1), seen) == seen)
            {
                return true;
            }
        }
    }
}
