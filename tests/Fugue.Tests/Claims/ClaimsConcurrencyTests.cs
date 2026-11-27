using FluentResults;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Claims;
using Fugue.Domain.Results;
using Fugue.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Tests.Claims;

/// <summary>The claim save is version-guarded even when a command changes nothing (a race must be a 412, never a stale success).</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ClaimsConcurrencyTests(PostgresFixture db)
{
    private readonly ClaimsHarness _h = new(db);

    [Fact]
    public async Task A_save_that_changes_nothing_still_fails_when_another_write_committed_since_the_load()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, $"/api/v1/claims/{claim.Id}/pickup", claim.Version);
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Put, $"/api/v1/claims/{claim.Id}/reserve", claim.Version, new { amountMinor = 100 });
        var scope = new ClaimScope(true, null);

        await using AsyncServiceScope first = db.Factory.Services.CreateAsyncScope();
        await using AsyncServiceScope second = db.Factory.Services.CreateAsyncScope();
        IClaimRepository firstRepo = first.ServiceProvider.GetRequiredService<IClaimRepository>();
        IClaimRepository secondRepo = second.ServiceProvider.GetRequiredService<IClaimRepository>();
        Claim loadedByFirst = (await firstRepo.FindForUpdateAsync(claim.Id, scope, default))!;
        Claim loadedBySecond = (await secondRepo.FindForUpdateAsync(claim.Id, scope, default))!;
        long version = loadedByFirst.RowVersion;

        Assert.True(loadedBySecond.SetReserve(200).IsSuccess);
        Assert.True((await secondRepo.SaveAsync(loadedBySecond, version, default)).IsSuccess);

        Assert.True(loadedByFirst.SetReserve(100).IsSuccess); // the value it loaded: EF sees no change
        Result stale = await firstRepo.SaveAsync(loadedByFirst, version, default);

        Assert.True(stale.IsFailed);
        Assert.Equal("claim.version_mismatch", Assert.IsType<FugueError>(stale.Errors[0]).Code);
    }
}
