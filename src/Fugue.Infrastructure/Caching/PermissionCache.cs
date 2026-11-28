using System.ComponentModel;
using Fugue.Application.Caching;
using Fugue.Application.Features.Me.Models;
using Fugue.Application.Features.Me.Repos;
using Fugue.Domain.Ids;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Caching;

/// <summary>
/// HybridCache-backed <c>/me</c> cache and its invalidator. The key is the user id alone; tags are <c>perm:user:{id}</c> and
/// <c>perm:all</c>. HybridCache coalesces concurrent misses for one key, so a stampede of one user's <c>/me</c> loads once.
/// </summary>
internal sealed class PermissionCache(HybridCache cache, IOptions<CacheOptions> options) : IMeCache, IPermissionCacheInvalidator
{
    internal const string AllTag = "perm:all";

    internal static string MeKey(UserId userId) => $"me:{userId.Value:N}";

    internal static string UserTag(UserId userId) => $"perm:user:{userId.Value:N}";

    public async ValueTask<MeDto?> GetOrCreateAsync(UserId userId, Func<CancellationToken, ValueTask<MeDto?>> load, CancellationToken ct)
    {
        TimeSpan ttl = TimeSpan.FromSeconds(options.Value.MePermissionsTtlSeconds);
        var entryOptions = new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = ttl };
        try
        {
            MeEntry entry = await cache.GetOrCreateAsync(
                MeKey(userId),
                load,
                static async (factory, token) => new MeEntry(await factory(token) ?? throw new NotCachedException()),
                entryOptions,
                [UserTag(userId), AllTag],
                ct);
            return entry.Value;
        }
        catch (NotCachedException)
        {
            return null;
        }
    }

    public ValueTask UserChangedAsync(UserId userId) => cache.RemoveByTagAsync(UserTag(userId), CancellationToken.None);

    public ValueTask AllChangedAsync() => cache.RemoveByTagAsync(AllTag, CancellationToken.None);

    /// <summary>Sealed and marked immutable so HybridCache keeps the instance in L1 instead of serializing a defensive copy.</summary>
    [ImmutableObject(true)]
    private sealed record MeEntry(MeDto Value);

    /// <summary>Thrown out of the factory so HybridCache stores nothing for an unknown or inactive user.</summary>
    private sealed class NotCachedException : Exception;
}
