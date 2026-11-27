using System.Net;
using System.Net.Http.Json;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Tests.Persistence;

namespace Fugue.Tests.Claims;

/// <summary>
/// A known dataset in its OWN database (other tests also create claims, so absolute numbers need an empty one), built through the API with the test clock
/// moved to each event. Report time is T = 2030-03-01T00:00Z. Two officers (A, B), one manager.
/// <code>
///  id  market line      submitted  state
///   1  SG     Motor     T-1d       Submitted (unassigned)
///   2  SG     Motor     T-5d       UnderReview (A), reserve 100000
///   3  SG     Property  T-10d      InfoRequested (A), reserve 50000
///   4  TH     Motor     T-20d      UnderReview (B), reserve 200000
///   5  SG     Motor     T-9d       Approved (A) at T-6d: reserve 80000, approved 60000
///   6  SG     Motor     T-30d      Rejected (A) at T-25d
///   7  ID     Property  T-3d       Settled (B): decided at T-2d, settled at T-1d
///   8  SG     Motor     T-60d      Rejected (A) at T-40d (outside the 30-day window)
///   9  SG     Property  T-3d       UnderReview (B), no reserve (exactly 3 days: the 3-7 band)
///  10  HK     Motor     T-2d23h    Submitted (unassigned) (just under 3 days: the 0-2 band)
///  11  TH     Property  T-15d      UnderReview (B), no reserve (exactly 15 days: the 15+ band)
/// </code>
/// </summary>
public sealed class ClaimReportsFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset T = new(2030, 3, 1, 0, 0, 0, TimeSpan.Zero);

    public PostgresFixture Db { get; } = new();

    public UserId OfficerA { get; private set; }

    public UserId OfficerB { get; private set; }

    public async Task InitializeAsync()
    {
        await Db.InitializeAsync();
        (string emailA, UserId a) = await Db.NewUserAsync([RoleCodes.Staff]);
        (string emailB, UserId b) = await Db.NewUserAsync([RoleCodes.Staff]);
        (string emailM, _) = await Db.NewUserAsync([RoleCodes.Manager]);
        OfficerA = a;
        OfficerB = b;
        var h = new ClaimsHarness(Db);
        using HttpClient self = h.Claimant();
        using HttpClient officerA = Db.ClientAs(emailA);
        using HttpClient officerB = Db.ClientAs(emailB);
        using HttpClient manager = Db.ClientAs(emailM);

        void At(TimeSpan before) => Db.Clock.Now = T - before;
        async Task<ClaimDto> Submit(TimeSpan before, Market m, LineOfBusiness l) { At(before); return await h.SubmitAsync(self, m, l); }
        async Task<ClaimDto> Pick(HttpClient officer, ClaimDto c, TimeSpan before) { At(before); return await ClaimsHarness.OkAsync(officer, HttpMethod.Post, $"/api/v1/claims/{c.Id}/pickup", c.Version); }
        async Task<ClaimDto> Reserve(HttpClient officer, ClaimDto c, long amount, TimeSpan before) { At(before); return await ClaimsHarness.OkAsync(officer, HttpMethod.Put, $"/api/v1/claims/{c.Id}/reserve", c.Version, new { amountMinor = amount }); }
        async Task<ClaimDto> Decide(HttpClient officer, ClaimDto c, object body, TimeSpan before) { At(before); return await ClaimsHarness.OkAsync(officer, HttpMethod.Post, $"/api/v1/claims/{c.Id}/decision", c.Version, body); }

        TimeSpan D(double days) => TimeSpan.FromDays(days);

        await Submit(D(1), Market.SG, LineOfBusiness.Motor); // 1

        ClaimDto c2 = await Submit(D(5), Market.SG, LineOfBusiness.Motor); // 2
        c2 = await Pick(officerA, c2, D(5));
        await Reserve(officerA, c2, 100_000, D(4.9));

        ClaimDto c3 = await Submit(D(10), Market.SG, LineOfBusiness.Property); // 3
        c3 = await Pick(officerA, c3, D(10));
        c3 = await Reserve(officerA, c3, 50_000, D(9.9));
        At(D(9.8));
        await ClaimsHarness.OkAsync(officerA, HttpMethod.Post, $"/api/v1/claims/{c3.Id}/info-requests", c3.Version, new { question = "Photos?" });

        ClaimDto c4 = await Submit(D(20), Market.TH, LineOfBusiness.Motor); // 4
        c4 = await Pick(officerB, c4, D(20));
        await Reserve(officerB, c4, 200_000, D(19.9));

        ClaimDto c5 = await Submit(D(9), Market.SG, LineOfBusiness.Motor); // 5
        c5 = await Pick(officerA, c5, D(9));
        c5 = await Reserve(officerA, c5, 80_000, D(8.9));
        await Decide(officerA, c5, new { outcome = "approve", amountMinor = 60_000, reason = "Covered." }, D(6));

        ClaimDto c6 = await Submit(D(30), Market.SG, LineOfBusiness.Motor); // 6
        c6 = await Pick(officerA, c6, D(30));
        await Decide(officerA, c6, new { outcome = "reject", reason = "Lapsed." }, D(25));

        ClaimDto c7 = await Submit(D(3), Market.ID, LineOfBusiness.Property); // 7
        c7 = await Pick(officerB, c7, D(3));
        c7 = await Reserve(officerB, c7, 9_000_000, D(2.9));
        c7 = await Decide(officerB, c7, new { outcome = "approve", amountMinor = 7_000_000, reason = "Covered." }, D(2));
        At(D(1));
        await ClaimsHarness.OkAsync(manager, HttpMethod.Post, $"/api/v1/claims/{c7.Id}/settle", c7.Version);

        ClaimDto c8 = await Submit(D(60), Market.SG, LineOfBusiness.Motor); // 8
        c8 = await Pick(officerA, c8, D(60));
        await Decide(officerA, c8, new { outcome = "reject", reason = "Lapsed." }, D(40));

        ClaimDto c9 = await Submit(D(3), Market.SG, LineOfBusiness.Property); // 9
        await Pick(officerB, c9, D(3));

        await Submit(D(2) + TimeSpan.FromHours(23), Market.HK, LineOfBusiness.Motor); // 10

        ClaimDto c11 = await Submit(D(15), Market.TH, LineOfBusiness.Property); // 11
        await Pick(officerB, c11, D(15));

        Db.Clock.Now = T;
    }

    public Task DisposeAsync() => Db.DisposeAsync();
}

public sealed class ClaimReportsTests(ClaimReportsFixture fx) : IClassFixture<ClaimReportsFixture>
{
    [Fact]
    public async Task Workload_counts_each_officers_open_claims_ages_decisions_and_median_on_the_known_dataset()
    {
        using HttpClient staff = fx.Db.ClientAs(DevUsers.Staff);

        ClaimWorkloadDto board = (await staff.GetFromJsonAsync<ClaimWorkloadDto>("/api/v1/claims/workload"))!;

        Assert.Equal(ClaimReportsFixture.T, board.AsOf);
        Assert.Equal(2, board.UnassignedCount); // claims 1 and 10
        Assert.Equal([("0-2", 2), ("3-7", 2), ("8-14", 1), ("15+", 2)], board.Ageing.Select(a => (a.Label, a.Count))); // 1,10 | 2,9 | 3 | 4,11

        OfficerWorkloadDto a = board.Officers.Single(o => o.OfficerId == fx.OfficerA);
        Assert.Equal((1, 1), (a.UnderReview, a.InfoRequested)); // claims 2 and 3
        Assert.Equal(10.0, a.OldestOpenAgeDays!.Value, precision: 6); // claim 3
        Assert.Equal((1, 2), (a.DecidedLast7Days, a.DecidedLast30Days)); // 5 at T-6d; 5 and 6 inside 30 days; 8 is outside
        Assert.Equal(4.0, a.MedianDaysToDecision!.Value, precision: 6); // claim 5: 3 days, claim 6: 5 days

        OfficerWorkloadDto b = board.Officers.Single(o => o.OfficerId == fx.OfficerB);
        Assert.Equal((3, 0), (b.UnderReview, b.InfoRequested)); // claims 4, 9, 11
        Assert.Equal(20.0, b.OldestOpenAgeDays!.Value, precision: 6); // claim 4
        Assert.Equal((1, 1), (b.DecidedLast7Days, b.DecidedLast30Days)); // claim 7 (settled claims still count as decided)
        Assert.Equal(1.0, b.MedianDaysToDecision!.Value, precision: 6);
    }

    [Fact]
    public async Task Workload_needs_the_workload_permission_and_a_claimant_is_refused()
    {
        using HttpClient self = fx.Db.ClientAs(DevUsers.Self);
        using HttpClient manager = fx.Db.ClientAs(DevUsers.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await self.GetAsync("/api/v1/claims/workload")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/v1/claims/workload")).StatusCode);
    }

    [Fact]
    public async Task Exposure_sums_open_reserve_and_approved_not_settled_per_market_line_and_currency()
    {
        using HttpClient manager = fx.Db.ClientAs(DevUsers.Admin);

        ClaimExposureDto exposure = (await manager.GetFromJsonAsync<ClaimExposureDto>("/api/v1/claims/exposure"))!;

        Assert.Equal(
            [
                (Market.SG, LineOfBusiness.Motor, "SGD", 2, 100_000L, 1, 60_000L), // open 1 and 2; approved claim 5 is NOT in the reserve
                (Market.SG, LineOfBusiness.Property, "SGD", 2, 50_000L, 0, 0L), // open 3 and 9
                (Market.TH, LineOfBusiness.Motor, "THB", 1, 200_000L, 0, 0L), // 4
                (Market.TH, LineOfBusiness.Property, "THB", 1, 0L, 0, 0L), // 11 has no reserve yet
                (Market.HK, LineOfBusiness.Motor, "HKD", 1, 0L, 0, 0L), // 10
            ],
            exposure.Rows.OrderBy(r => r.Currency == "SGD" ? 0 : r.Currency == "THB" ? 1 : 2).ThenBy(r => r.Line)
                .Select(r => (r.Market, r.Line, r.Currency, r.OpenCount, r.OutstandingReserveMinor, r.ApprovedNotSettledCount, r.ApprovedNotSettledMinor)));
    }

    [Fact]
    public async Task Exposure_totals_are_per_currency_never_across_currencies()
    {
        using HttpClient manager = fx.Db.ClientAs(DevUsers.Admin);

        ClaimExposureDto exposure = (await manager.GetFromJsonAsync<ClaimExposureDto>("/api/v1/claims/exposure"))!;

        Assert.Equal(["HKD", "SGD", "THB"], exposure.Totals.Select(t => t.Currency));
        ExposureTotalDto sgd = exposure.Totals.Single(t => t.Currency == "SGD");
        ExposureTotalDto thb = exposure.Totals.Single(t => t.Currency == "THB");
        Assert.Equal((4, 150_000L, 1, 60_000L), (sgd.OpenCount, sgd.OutstandingReserveMinor, sgd.ApprovedNotSettledCount, sgd.ApprovedNotSettledMinor));
        Assert.Equal((2, 200_000L, 0, 0L), (thb.OpenCount, thb.OutstandingReserveMinor, thb.ApprovedNotSettledCount, thb.ApprovedNotSettledMinor));
        Assert.DoesNotContain(exposure.Totals, t => t.OutstandingReserveMinor == 350_000); // 150000 SGD + 200000 THB must never be one number
        Assert.DoesNotContain(exposure.Rows, r => r.Currency == "IDR"); // the only ID claim is settled: no exposure
    }

    [Fact]
    public async Task Terminal_claims_never_count_as_exposure()
    {
        using HttpClient manager = fx.Db.ClientAs(DevUsers.Admin);

        ClaimExposureDto exposure = (await manager.GetFromJsonAsync<ClaimExposureDto>("/api/v1/claims/exposure"))!;

        // Open (still to decide): claims 1, 2, 3, 4, 9, 10, 11. Approved awaiting settlement: claim 5. Rejected (6, 8) and settled (7) are not exposure.
        Assert.Equal(7, exposure.Rows.Sum(r => r.OpenCount));
        Assert.Equal(1, exposure.Rows.Sum(r => r.ApprovedNotSettledCount));
    }

    [Fact]
    public async Task Exposure_needs_the_exposure_permission_staff_and_claimants_are_refused()
    {
        using HttpClient staff = fx.Db.ClientAs(DevUsers.Staff);
        using HttpClient self = fx.Db.ClientAs(DevUsers.Self);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/claims/exposure")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await self.GetAsync("/api/v1/claims/exposure")).StatusCode);
    }
}
