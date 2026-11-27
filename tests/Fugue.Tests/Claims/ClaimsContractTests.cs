using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;

namespace Fugue.Tests.Claims;

/// <summary>The claims contract over HTTP against real Postgres: submit, read, list, timeline and the claimant/staff views of the same claim.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ClaimsContractTests(PostgresFixture db)
{
    private readonly ClaimsHarness _h = new(db);

    [Fact]
    public async Task Submit_creates_a_Submitted_claim_with_location_etag_number_and_the_markets_currency()
    {
        using HttpClient self = _h.Claimant();

        HttpResponseMessage response = await ClaimsHarness.PostSubmitAsync(self, new
        {
            market = "ID",
            line = "Property",
            policyNumber = "  POL-77  ",
            incidentDate = _h.Yesterday().Iso(),
            incidentDescription = "Water damage.",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ClaimDto claim = (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
        Assert.Equal($"/api/v1/claims/{claim.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal($"\"{claim.Version}\"", response.Headers.ETag!.Tag);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal("IDR", claim.Currency);
        Assert.Equal("POL-77", claim.PolicyNumber);
        Assert.Matches(@"^CLM-ID-\d{4}-\d{6}$", claim.Number);
        Assert.Null(claim.ReserveMinor);
        Assert.Null(claim.ClaimantUserId); // the staff group is hidden from the claimant
    }

    [Fact]
    public async Task Submit_without_an_Idempotency_Key_is_400_and_creates_nothing_while_customers_create_stays_optional()
    {
        using HttpClient self = _h.Claimant();
        string policy = ClaimsHarness.UniquePolicy();

        HttpResponseMessage missing = await self.PostAsJsonAsync("/api/v1/claims", new { market = "SG", line = "Motor", policyNumber = policy, incidentDate = _h.Yesterday().Iso(), incidentDescription = "x" });

        await ClaimsHarness.AssertProblemAsync(missing, HttpStatusCode.BadRequest, "idempotency.key_required");
        Assert.Equal(0L, await _h.ScalarAsync<long>("SELECT count(*) FROM claims WHERE policy_number = @p", ("p", policy)));

        using HttpClient admin = _h.Admin();
        HttpResponseMessage customer = await admin.PostAsJsonAsync("/api/v1/customers", new { name = "No Key", email = $"nokey-{Guid.NewGuid():N}@example.test" });
        Assert.Equal(HttpStatusCode.Created, customer.StatusCode);
    }

    [Fact]
    public async Task A_replayed_submit_with_the_same_idempotency_key_returns_the_same_claim_and_creates_one_row()
    {
        using HttpClient self = _h.Claimant();
        string key = Guid.NewGuid().ToString("N");
        string policy = ClaimsHarness.UniquePolicy();
        object body = new { market = "SG", line = "Motor", policyNumber = policy, incidentDate = _h.Yesterday().Iso(), incidentDescription = "x" };

        async Task<HttpResponseMessage> Submit()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/claims") { Content = JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", $"\"{key}\"");
            return await self.SendAsync(request);
        }

        HttpResponseMessage first = await Submit();
        HttpResponseMessage replay = await Submit();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<ClaimDto>())!.Id, (await replay.Content.ReadFromJsonAsync<ClaimDto>())!.Id);
        Assert.Equal(1L, await _h.ScalarAsync<long>("SELECT count(*) FROM claims WHERE policy_number = @p", ("p", policy)));
    }

    [Fact]
    public async Task A_claimant_never_sees_the_reserve_or_the_officer_but_staff_do()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient staff = _h.Officer();
        ClaimDto submitted = await _h.SubmitAsync(self);
        ClaimDto picked = await ClaimsHarness.OkAsync(staff, HttpMethod.Post, $"/api/v1/claims/{submitted.Id}/pickup", submitted.Version);
        ClaimDto reserved = await ClaimsHarness.OkAsync(staff, HttpMethod.Put, $"/api/v1/claims/{submitted.Id}/reserve", picked.Version, new { amountMinor = 250_000 });

        ClaimDto asClaimant = (await self.GetFromJsonAsync<ClaimDto>($"/api/v1/claims/{submitted.Id}"))!;
        ClaimDto asStaff = (await staff.GetFromJsonAsync<ClaimDto>($"/api/v1/claims/{submitted.Id}"))!;
        string raw = await self.GetStringAsync($"/api/v1/claims/{submitted.Id}");

        Assert.Equal(ClaimStatus.UnderReview, asClaimant.Status);
        Assert.Null(asClaimant.ReserveMinor);
        Assert.Null(asClaimant.AssignedOfficerId);
        Assert.Null(asClaimant.AssignedOfficerName);
        Assert.Equal(250_000, asStaff.ReserveMinor);
        Assert.Equal(reserved.Version, asStaff.Version);
        Assert.NotNull(asStaff.AssignedOfficerId);
        Assert.Equal("Dev Staff", asStaff.AssignedOfficerName);
        Assert.Equal("Dev Self-Service One", asStaff.ClaimantName);
        Assert.DoesNotContain("250000", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_claimant_lists_only_their_own_claims_newest_first_and_staff_list_all_oldest_first()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient self2 = _h.Claimant(DevUsers.Self2);
        using HttpClient staff = _h.Officer();
        string policy = ClaimsHarness.UniquePolicy();
        var mine = new List<ClaimDto>();
        for (int i = 0; i < 3; i++)
        {
            db.Clock.Now = db.Clock.Now.AddSeconds(1); // distinct submitted_at so the order is the submission order
            mine.Add(await _h.SubmitAsync(self, policy: policy));
        }

        db.Clock.Now = db.Clock.Now.AddSeconds(1);
        ClaimDto theirs = await _h.SubmitAsync(self2, policy: policy);

        ClaimDto[] asClaimant = (await self.GetFromJsonAsync<ClaimDto[]>($"/api/v1/claims?q={policy}"))!;
        ClaimDto[] asOther = (await self2.GetFromJsonAsync<ClaimDto[]>($"/api/v1/claims?q={policy}"))!;
        ClaimDto[] asStaff = (await staff.GetFromJsonAsync<ClaimDto[]>($"/api/v1/claims?q={policy}"))!;

        Assert.Equal(mine.AsEnumerable().Reverse().Select(c => c.Id), asClaimant.Select(c => c.Id)); // newest first, theirs excluded
        Assert.Equal([theirs.Id], asOther.Select(c => c.Id));
        Assert.Equal(mine.Select(c => c.Id).Append(theirs.Id), asStaff.Select(c => c.Id)); // oldest first
    }

    [Fact]
    public async Task Keyset_paging_walks_both_directions_via_link_and_next_cursor()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient staff = _h.Officer();
        string policy = ClaimsHarness.UniquePolicy();
        var created = new List<ClaimDto>();
        for (int i = 0; i < 5; i++)
        {
            db.Clock.Now = db.Clock.Now.AddSeconds(1);
            created.Add(await _h.SubmitAsync(self, policy: policy));
        }

        foreach ((HttpClient client, ClaimDto[] expected) in new[] { (staff, created.ToArray()), (self, created.AsEnumerable().Reverse().ToArray()) })
        {
            var seen = new List<Guid>();
            string? url = $"/api/v1/claims?limit=2&q={policy}";
            while (url is not null)
            {
                HttpResponseMessage page = await client.GetAsync(url);
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                seen.AddRange((await page.Content.ReadFromJsonAsync<ClaimDto[]>())!.Select(c => c.Id.Value));
                url = page.Headers.TryGetValues("Link", out IEnumerable<string>? link) ? link.Single()[1..link.Single().IndexOf('>', StringComparison.Ordinal)] : null;
            }

            Assert.Equal(expected.Select(c => c.Id.Value), seen);
        }
    }

    [Fact]
    public async Task List_filters_by_status_assignment_market_line_and_search()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient staff = _h.Officer();
        string policy = ClaimsHarness.UniquePolicy();
        ClaimDto a = await _h.SubmitAsync(self, Market.SG, LineOfBusiness.Motor, policy);
        ClaimDto b = await _h.SubmitAsync(self, Market.TH, LineOfBusiness.Property, policy);
        await ClaimsHarness.OkAsync(staff, HttpMethod.Post, $"/api/v1/claims/{b.Id}/pickup", b.Version);

        async Task<Guid[]> Ids(string query) => [.. (await staff.GetFromJsonAsync<ClaimDto[]>($"/api/v1/claims?q={policy}&{query}"))!.Select(c => c.Id.Value)];

        Assert.Equal([a.Id.Value], await Ids("status=Submitted"));
        Assert.Equal([b.Id.Value], await Ids("status=UnderReview"));
        Assert.Equal(2, (await Ids("status=Submitted&status=UnderReview")).Length);
        Assert.Equal(2, (await Ids("status=Submitted,UnderReview")).Length);
        Assert.Equal([b.Id.Value], await Ids("assigned=me"));
        Assert.Equal([a.Id.Value], await Ids("assigned=none"));
        Assert.Equal(2, (await Ids("assigned=any")).Length);
        Assert.Equal([a.Id.Value], await Ids("market=SG"));
        Assert.Equal([b.Id.Value], await Ids("line=Property"));
        Assert.Equal([a.Id.Value], (await staff.GetFromJsonAsync<ClaimDto[]>($"/api/v1/claims?q={a.Number}"))!.Select(c => c.Id.Value));
    }

    [Theory]
    [InlineData("status=Bogus")]
    [InlineData("assigned=nobody")]
    [InlineData("market=XX")]
    [InlineData("line=Marine")]
    [InlineData("limit=0")]
    [InlineData("cursor=not-a-cursor")]
    public async Task A_bad_list_filter_is_a_400_problem(string query)
    {
        using HttpClient staff = _h.Officer();

        HttpResponseMessage response = await staff.GetAsync($"/api/v1/claims?{query}");

        await ClaimsHarness.AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task The_timeline_is_ordered_and_a_claimant_sees_staff_actions_as_the_claims_team()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient staff = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);
        await ClaimsHarness.OkAsync(staff, HttpMethod.Post, $"/api/v1/claims/{claim.Id}/pickup", claim.Version);

        ClaimTimelineEntryDto[] asClaimant = (await self.GetFromJsonAsync<ClaimTimelineEntryDto[]>($"/api/v1/claims/{claim.Id}/timeline"))!;
        ClaimTimelineEntryDto[] asStaff = (await staff.GetFromJsonAsync<ClaimTimelineEntryDto[]>($"/api/v1/claims/{claim.Id}/timeline"))!;

        Assert.Equal([ClaimStatus.Submitted, ClaimStatus.UnderReview], asClaimant.Select(e => e.ToStatus));
        Assert.Equal(["Dev Self-Service One", "Claims team"], asClaimant.Select(e => e.ActorDisplay));
        Assert.Equal(["Dev Self-Service One", "Dev Staff"], asStaff.Select(e => e.ActorDisplay));
        Assert.Null(asClaimant[0].FromStatus);
        Assert.Equal(ClaimStatus.Submitted, asClaimant[1].FromStatus);
    }

    [Theory]
    [InlineData("""{"market":"SG","line":"Motor","policyNumber":"","incidentDate":"{D}","incidentDescription":"x"}""", "policyNumber")]
    [InlineData("""{"market":"SG","line":"Motor","policyNumber":"P","incidentDate":"{D}","incidentDescription":""}""", "incidentDescription")]
    [InlineData("""{"market":"SG","line":"Motor","policyNumber":"P","incidentDate":"2099-01-01","incidentDescription":"x"}""", "incidentDate")]
    [InlineData("""{"market":"SG","line":"Motor","policyNumber":"P","incidentDate":"1990-01-01","incidentDescription":"x"}""", "incidentDate")]
    public async Task Submit_validates_its_fields_and_the_incident_window(string json, string field)
    {
        using HttpClient self = _h.Claimant();
        using var content = new StringContent(json.Replace("{D}", _h.Yesterday().Iso(), StringComparison.Ordinal), System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await ClaimsHarness.PostSubmitAsync(self, content);

        await ClaimsHarness.AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"expected an error on {field}");
    }

    [Fact]
    public async Task An_incident_dated_today_in_the_market_is_accepted_and_one_day_ahead_is_not()
    {
        using HttpClient self = _h.Claimant();
        DateOnly today = Markets.LocalToday(Market.SG, db.Clock.GetUtcNow());

        HttpResponseMessage ok = await ClaimsHarness.PostSubmitAsync(self, new { market = "SG", line = "Motor", policyNumber = "P", incidentDate = today.Iso(), incidentDescription = "x" });
        HttpResponseMessage tomorrow = await ClaimsHarness.PostSubmitAsync(self, new { market = "SG", line = "Motor", policyNumber = "P", incidentDate = today.AddDays(1).Iso(), incidentDescription = "x" });
        HttpResponseMessage yearAgo = await ClaimsHarness.PostSubmitAsync(self, new { market = "SG", line = "Motor", policyNumber = "P", incidentDate = today.AddYears(-1).Iso(), incidentDescription = "x" });
        HttpResponseMessage tooOld = await ClaimsHarness.PostSubmitAsync(self, new { market = "SG", line = "Motor", policyNumber = "P", incidentDate = today.AddYears(-1).AddDays(-1).Iso(), incidentDescription = "x" });

        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tomorrow.StatusCode);
        Assert.Equal(HttpStatusCode.Created, yearAgo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooOld.StatusCode);
    }
}
