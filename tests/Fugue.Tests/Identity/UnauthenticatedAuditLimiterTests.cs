using System.Reflection;
using Fugue.Api.Auth;
using Fugue.Infrastructure.Audit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Fugue.Tests.Identity;

/// <summary>The 401 audit limiter in isolation: fixed memory, per-bucket caps, window renewal, collisions only ever suppress.</summary>
public sealed class UnauthenticatedAuditLimiterTests
{
    private static UnauthenticatedAuditLimiter Make(int perMinute, int buckets, TimeProvider time) =>
        new(Options.Create(new AuditOptions { UnauthenticatedRowsPerIpPerMinute = perMinute, UnauthenticatedBuckets = buckets }), time);

    private static (string A, string B) TwoIpsInDifferentBuckets(UnauthenticatedAuditLimiter limiter)
    {
        string a = "10.0.0.1";
        string b = Enumerable.Range(2, 1000).Select(i => "10.0.0." + i).First(ip => limiter.BucketFor(ip) != limiter.BucketFor(a));
        return (a, b);
    }

    [Fact]
    public void A_spray_of_100000_distinct_addresses_never_grows_the_structure()
    {
        UnauthenticatedAuditLimiter limiter = Make(5, 4096, new FakeTimeProvider());
        string[] addresses = Enumerable.Range(0, 100_000).Select(i => $"2001:db8::{i:x}:{i * 7:x}").ToArray();
        FieldInfo[] fields = typeof(UnauthenticatedAuditLimiter).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        long before = GC.GetAllocatedBytesForCurrentThread();
        foreach (string address in addresses)
        {
            limiter.TryAcquire(address);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(4096, limiter.BucketCount);
        Assert.True(allocated < 64 * 1024, $"allocated {allocated} bytes for 100000 addresses");
        // The only collection the type holds is the one fixed bucket array; nothing keyed by address exists.
        Assert.DoesNotContain(fields, f => f.FieldType.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(f.FieldType));
        Assert.Single(fields, f => f.FieldType == typeof(long[]));
    }

    [Fact]
    public void Each_bucket_has_its_own_cap_so_one_noisy_address_does_not_silence_another()
    {
        var limiter = Make(2, 4096, new FakeTimeProvider());
        (string a, string b) = TwoIpsInDifferentBuckets(limiter);

        Assert.True(limiter.TryAcquire(a));
        Assert.True(limiter.TryAcquire(a));
        Assert.False(limiter.TryAcquire(a));
        Assert.True(limiter.TryAcquire(b));
        Assert.True(limiter.TryAcquire(b));
        Assert.False(limiter.TryAcquire(b));
    }

    [Fact]
    public void Colliding_addresses_share_one_bucket_which_only_suppresses()
    {
        var limiter = Make(3, 1, new FakeTimeProvider()); // one bucket: every address collides

        int written = Enumerable.Range(0, 50).Count(i => limiter.TryAcquire("192.0.2." + i));

        Assert.Equal(3, written);
    }

    [Fact]
    public void The_window_renews_after_a_minute_and_not_before()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 27, 12, 0, 10, TimeSpan.Zero));
        var limiter = Make(2, 64, time);

        Assert.True(limiter.TryAcquire("198.51.100.1"));
        Assert.True(limiter.TryAcquire("198.51.100.1"));
        Assert.False(limiter.TryAcquire("198.51.100.1"));
        time.Advance(TimeSpan.FromSeconds(20)); // still the 12:00 minute
        Assert.False(limiter.TryAcquire("198.51.100.1"));
        time.Advance(TimeSpan.FromSeconds(31)); // 12:01
        Assert.True(limiter.TryAcquire("198.51.100.1"));
        Assert.True(limiter.TryAcquire("198.51.100.1"));
        Assert.False(limiter.TryAcquire("198.51.100.1"));
    }

    [Fact]
    public void Concurrent_acquisitions_never_exceed_the_cap()
    {
        var limiter = Make(10, 8, new FakeTimeProvider());
        int granted = 0;

        Parallel.For(0, 1000, _ =>
        {
            if (limiter.TryAcquire("203.0.113.9"))
            {
                Interlocked.Increment(ref granted);
            }
        });

        Assert.Equal(10, granted);
    }

    [Fact]
    public void A_cap_of_zero_grants_nothing()
    {
        Assert.False(Make(0, 8, new FakeTimeProvider()).TryAcquire("203.0.113.9"));
    }

    [Fact]
    public void A_null_address_is_one_shared_unknown_bucket()
    {
        var limiter = Make(1, 64, new FakeTimeProvider());

        Assert.True(limiter.TryAcquire(null));
        Assert.False(limiter.TryAcquire(null));
    }
}
