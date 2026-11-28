using Fugue.Application.Caching;
using Fugue.Application.Features.Me.Models;
using Fugue.Application.Features.Me.Repos;
using Fugue.Domain.Ids;
using Fugue.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Tests.Infrastructure;

/// <summary>The <c>/me</c> HybridCache as the composition root registers it: stampede coalescing, per-user keys, tag invalidation.</summary>
public sealed class PermissionCacheTests : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly IMeCache cache;
    private readonly IPermissionCacheInvalidator invalidator;

    public PermissionCacheTests()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused" })
            .Build();
        provider = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
        cache = provider.GetRequiredService<IMeCache>();
        invalidator = provider.GetRequiredService<IPermissionCacheInvalidator>();
    }

    public void Dispose() => provider.Dispose();

    private static MeDto Dto(UserId id, string email, params string[] permissions) =>
        new(id, email, email, null, ["STAFF"], [], permissions, []);

    [Fact]
    public async Task Two_hundred_concurrent_misses_for_one_user_run_the_loader_once()
    {
        var user = UserId.New();
        int calls = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<MeDto?> Load(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            await gate.Task.WaitAsync(ct);
            return Dto(user, "a@x.test");
        }

        Task<MeDto?>[] readers = [.. Enumerable.Range(0, 200).Select(_ => cache.GetOrCreateAsync(user, Load, CancellationToken.None).AsTask())];
        await Task.Delay(100);
        gate.SetResult();
        MeDto?[] results = await Task.WhenAll(readers);

        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Equal(user, r!.UserId));
    }

    [Fact]
    public async Task Each_user_reads_only_their_own_entry()
    {
        UserId a = UserId.New(), b = UserId.New();
        await cache.GetOrCreateAsync(a, _ => ValueTask.FromResult<MeDto?>(Dto(a, "a@x.test", "AUDIT_READ")), CancellationToken.None);

        MeDto? forB = await cache.GetOrCreateAsync(b, _ => ValueTask.FromResult<MeDto?>(Dto(b, "b@x.test")), CancellationToken.None);
        MeDto? forA = await cache.GetOrCreateAsync(a, _ => throw new InvalidOperationException("a is cached"), CancellationToken.None);

        Assert.Equal("b@x.test", forB!.Email);
        Assert.Empty(forB.Permissions);
        Assert.Equal("a@x.test", forA!.Email);
    }

    [Fact]
    public async Task A_user_write_drops_only_that_user_and_a_global_write_drops_everyone()
    {
        UserId a = UserId.New(), b = UserId.New();
        int loadsA = 0, loadsB = 0;
        ValueTask<MeDto?> LoadA(CancellationToken _) => ValueTask.FromResult<MeDto?>(Dto(a, $"a{Interlocked.Increment(ref loadsA)}@x.test"));
        ValueTask<MeDto?> LoadB(CancellationToken _) => ValueTask.FromResult<MeDto?>(Dto(b, $"b{Interlocked.Increment(ref loadsB)}@x.test"));
        await cache.GetOrCreateAsync(a, LoadA, CancellationToken.None);
        await cache.GetOrCreateAsync(b, LoadB, CancellationToken.None);

        await invalidator.UserChangedAsync(a);
        Assert.Equal("a2@x.test", (await cache.GetOrCreateAsync(a, LoadA, CancellationToken.None))!.Email);
        Assert.Equal("b1@x.test", (await cache.GetOrCreateAsync(b, LoadB, CancellationToken.None))!.Email);

        await invalidator.AllChangedAsync();
        Assert.Equal("a3@x.test", (await cache.GetOrCreateAsync(a, LoadA, CancellationToken.None))!.Email);
        Assert.Equal("b2@x.test", (await cache.GetOrCreateAsync(b, LoadB, CancellationToken.None))!.Email);
    }

    [Fact]
    public async Task An_unknown_or_inactive_user_is_not_cached()
    {
        var user = UserId.New();
        int calls = 0;
        ValueTask<MeDto?> Load(CancellationToken _) => ValueTask.FromResult(Interlocked.Increment(ref calls) == 1 ? null : Dto(user, "back@x.test"));

        Assert.Null(await cache.GetOrCreateAsync(user, Load, CancellationToken.None));
        Assert.Equal("back@x.test", (await cache.GetOrCreateAsync(user, Load, CancellationToken.None))!.Email);
        Assert.Equal(2, calls);
    }
}
