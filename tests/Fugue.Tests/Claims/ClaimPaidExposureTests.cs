using System.Net;
using System.Net.Http.Json;
using Fugue.Application.Features.Claims.Models;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;

namespace Fugue.Tests.Claims;

/// <summary>
/// What has been paid out (backlog #009), in its OWN database so the sums are exact. Built through the API as a manager would settle.
/// <code>
///  market line   state                                   money
///  SG     Motor  Settled                                 approved 60000 (reserve 80000)
///  SG     Motor  Settled                                 approved 25000 (reserve 30000)
///  SG     Motor  Approved, not settled                   approved 40000 (reserve 50000)
///  SG     Motor  UnderReview                             reserve 100000
///  SG     Motor  Rejected                                none
///  TH     Motor  Settled                                 approved 1800000 (reserve 2000000): a row with only paid money
/// </code>
/// </summary>
public sealed class ClaimPaidExposureFixture : IAsyncLifetime
{
    public PostgresFixture Db { get; } = new();

    public string ManagerEmail { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Db.InitializeAsync();
        (string officerEmail, _) = await Db.NewUserAsync([RoleCodes.Staff]);
        (ManagerEmail, _) = await Db.NewUserAsync([RoleCodes.Manager]);
        var h = new ClaimsHarness(Db);
        using HttpClient self = h.Claimant();
        using HttpClient officer = Db.ClientAs(officerEmail);
        using HttpClient manager = Db.ClientAs(ManagerEmail);

        async Task<ClaimDto> Reserved(Market m, long reserve)
        {
            ClaimDto c = await h.SubmitAsync(self, m, LineOfBusiness.Motor);
            c = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, $"/api/v1/claims/{c.Id}/pickup", c.Version);
            return await ClaimsHarness.OkAsync(officer, HttpMethod.Put, $"/api/v1/claims/{c.Id}/reserve", c.Version, new { amountMinor = reserve });
        }

        async Task<ClaimDto> Approved(Market m, long reserve, long amount)
        {
            ClaimDto c = await Reserved(m, reserve);
            return await ClaimsHarness.OkAsync(officer, HttpMethod.Post, $"/api/v1/claims/{c.Id}/decision", c.Version, new { outcome = "approve", amountMinor = amount, reason = "Covered." });
        }

        async Task Settled(Market m, long reserve, long amount)
        {
            ClaimDto c = await Approved(m, reserve, amount);
            await ClaimsHarness.OkAsync(manager, HttpMethod.Post, $"/api/v1/claims/{c.Id}/settle", c.Version);
        }

        await Settled(Market.SG, 80_000, 60_000);
        await Settled(Market.SG, 30_000, 25_000);
        await Approved(Market.SG, 50_000, 40_000);
        await Reserved(Market.SG, 100_000);
        ClaimDto rejected = await h.SubmitAsync(self, Market.SG, LineOfBusiness.Motor);
        rejected = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, $"/api/v1/claims/{rejected.Id}/pickup", rejected.Version);
        await ClaimsHarness.OkAsync(officer, HttpMethod.Post, $"/api/v1/claims/{rejected.Id}/decision", rejected.Version, new { outcome = "reject", reason = "Lapsed." });
        await Settled(Market.TH, 2_000_000, 1_800_000);
    }

    public Task DisposeAsync() => Db.DisposeAsync();
}

public sealed class ClaimPaidExposureTests(ClaimPaidExposureFixture fx) : IClassFixture<ClaimPaidExposureFixture>
{
    [Fact]
    public async Task Exposure_shows_the_paid_sum_and_count_per_market_and_currency_next_to_the_open_and_approved_figures()
    {
        using HttpClient manager = fx.Db.ClientAs(fx.ManagerEmail);

        HttpResponseMessage response = await manager.GetAsync("/api/v1/claims/exposure");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ClaimExposureDto exposure = (await response.Content.ReadFromJsonAsync<ClaimExposureDto>())!;

        Assert.Equal(
            [
                // open 100000 reserve; approved-not-settled 40000; paid = 60000 + 25000. The rejected claim and the settled reserves count nowhere.
                (Market.SG, "SGD", 1, 100_000L, 1, 40_000L, 0, 2, 85_000L),
                // only settled claims: no exposure, but the paid money is still reported.
                (Market.TH, "THB", 0, 0L, 0, 0L, 0, 1, 1_800_000L),
            ],
            exposure.Rows.Select(r => (r.Market, r.Currency, r.OpenCount, r.OutstandingReserveMinor, r.ApprovedNotSettledCount, r.ApprovedNotSettledMinor,
                r.UnreservedOpenCount, r.SettledCount, r.SettledMinor)));
        Assert.All(exposure.Rows, r => Assert.Equal(LineOfBusiness.Motor, r.Line));
    }

    [Fact]
    public async Task Paid_totals_are_per_currency_never_across_currencies()
    {
        using HttpClient manager = fx.Db.ClientAs(fx.ManagerEmail);

        ClaimExposureDto exposure = (await manager.GetFromJsonAsync<ClaimExposureDto>("/api/v1/claims/exposure"))!;

        Assert.Equal([("SGD", 2, 85_000L), ("THB", 1, 1_800_000L)], exposure.Totals.Select(t => (t.Currency, t.SettledCount, t.SettledMinor)));
        Assert.DoesNotContain(exposure.Totals, t => t.SettledMinor == 1_885_000); // 85000 SGD + 1800000 THB must never be one number
    }

    [Fact]
    public async Task Staff_and_claimants_are_still_refused_the_exposure_with_paid_figures()
    {
        using HttpClient staff = fx.Db.ClientAs(DevUsers.Staff);
        using HttpClient self = fx.Db.ClientAs(DevUsers.Self);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/claims/exposure")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await self.GetAsync("/api/v1/claims/exposure")).StatusCode);
    }
}
