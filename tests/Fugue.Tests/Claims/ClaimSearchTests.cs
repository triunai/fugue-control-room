using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Search;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Tests.Claims;

/// <summary>
/// GET /api/v1/claims/search against a real Meilisearch (same pinned image as docker-compose.yml, D-063): a claimant's search never returns another
/// claimant's claim (the scope is an engine filter), staff see every claim, a changed claim is re-indexed, an empty q is 400 and with no engine
/// configured (or an unreachable one) the answer is 503.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ClaimSearchTests(PostgresFixture db) : IAsyncLifetime
{
    // Keep in step with the meilisearch service in docker-compose.yml.
    private const string Image = "getmeili/meilisearch:v1.12.8";
    private const string MasterKey = "test-master-key-0123456789abcdef"; // secrets-scan:ignore

    private readonly IContainer _meili = new ContainerBuilder(Image)
        .WithPortBinding(7700, true)
        .WithEnvironment("MEILI_MASTER_KEY", MasterKey)
        .WithEnvironment("MEILI_ENV", "development")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(7700).ForPath("/health")))
        .Build();

    private readonly ClaimsHarness _h = new(db);
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _meili.StartAsync();
        _factory = Engine($"http://{_meili.Hostname}:{_meili.GetMappedPublicPort(7700)}");
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _meili.DisposeAsync();
    }

    private WebApplicationFactory<Program> Engine(string url) => db.Factory.WithWebHostBuilder(b =>
    {
        b.UseSetting("Search:Meilisearch:Url", url);
        b.UseSetting("Search:Meilisearch:ApiKey", MasterKey);
    });

    private async Task<HttpClient> ClientAsync(WebApplicationFactory<Program> factory, string email)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await db.TokenAsync(email));
        return client;
    }

    private static Task<int> IndexAsync(WebApplicationFactory<Program> factory) =>
        factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<ClaimIndexer>().Single().IndexOnceAsync(default);

    private static async Task<string[]> NumbersAsync(HttpClient client, string q)
    {
        HttpResponseMessage response = await client.GetAsync("/api/v1/claims/search?q=" + Uri.EscapeDataString(q));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return [.. body.GetProperty("hits").EnumerateArray().Select(h => h.GetProperty("number").GetString()!)];
    }

    [Fact]
    public async Task A_claimants_search_never_returns_another_claimants_claim_and_staff_find_both()
    {
        HttpClient self = await ClientAsync(_factory, DevUsers.Self);
        HttpClient self2 = await ClientAsync(_factory, DevUsers.Self2);
        HttpClient staff = await ClientAsync(_factory, DevUsers.Staff);
        string shared = "Zorbulax" + Guid.NewGuid().ToString("N")[..6]; // a rare word both claims carry in the description
        ClaimDto mine = await SubmitAsync(self, shared);
        ClaimDto theirs = await SubmitAsync(self2, shared);
        await IndexAsync(_factory);

        // Searching by the other claimant's exact claim number and policy never returns it to me (typo tolerance may still surface my own similar
        // numbers), and the shared word finds only my claim.
        Assert.DoesNotContain(theirs.Number, await NumbersAsync(self, theirs.Number));
        Assert.DoesNotContain(theirs.Number, await NumbersAsync(self, theirs.PolicyNumber));
        Assert.DoesNotContain(theirs.Number, await NumbersAsync(self, "Orchard"));
        Assert.Equal([mine.Number], await NumbersAsync(self, shared));
        Assert.Equal([theirs.Number], await NumbersAsync(self2, shared));

        string[] seenByStaff = await NumbersAsync(staff, shared);
        Assert.Contains(mine.Number, seenByStaff);
        Assert.Contains(theirs.Number, seenByStaff);
    }

    [Fact]
    public async Task Search_matches_policy_place_and_description_and_a_changed_claim_is_reindexed()
    {
        HttpClient self = await ClientAsync(_factory, DevUsers.Self);
        HttpClient staff = await ClientAsync(_factory, DevUsers.Staff);
        ClaimDto claim = await SubmitAsync(self, "Hailstorm dented the bonnet");
        await IndexAsync(_factory);

        Assert.Contains(claim.Number, await NumbersAsync(self, claim.PolicyNumber));
        Assert.Contains(claim.Number, await NumbersAsync(staff, "Hailstorm"));
        Assert.Contains(claim.Number, await NumbersAsync(staff, "Orchard"));

        db.Clock.Now = db.Clock.Now.AddMinutes(5); // the fixture clock is frozen; a real change gets a later updated_at, which is what the cursor follows
        HttpResponseMessage pickup = await ClaimsHarness.SendAsync(staff, HttpMethod.Post, $"/api/v1/claims/{claim.Id}/pickup", claim.Version);
        Assert.Equal(HttpStatusCode.OK, pickup.StatusCode);
        Assert.True(await IndexAsync(_factory) >= 1);

        HttpResponseMessage hits = await staff.GetAsync("/api/v1/claims/search?q=" + claim.Number);
        JsonElement hit = (await hits.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hits").EnumerateArray().First();
        Assert.Equal("UnderReview", hit.GetProperty("status").GetString());
        Assert.False(hit.TryGetProperty("claimantUserId", out _));
    }

    [Fact]
    public async Task An_empty_q_is_400_validation()
    {
        HttpClient self = await ClientAsync(_factory, DevUsers.Self);

        Assert.Equal(HttpStatusCode.BadRequest, (await self.GetAsync("/api/v1/claims/search")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await self.GetAsync("/api/v1/claims/search?q=%20%20")).StatusCode);
    }

    [Fact]
    public async Task Without_a_search_engine_configured_the_endpoint_answers_503_search_unavailable()
    {
        HttpResponseMessage response = await db.ClientAs(DevUsers.Self).GetAsync("/api/v1/claims/search?q=anything");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("search.unavailable", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unreachable_engine_answers_503_search_unavailable_too()
    {
        using WebApplicationFactory<Program> broken = Engine("http://127.0.0.1:1");
        HttpClient self = await ClientAsync(broken, DevUsers.Self);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await self.GetAsync("/api/v1/claims/search?q=anything")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/v1/claims/search?q=x")).StatusCode);
    }

    [Fact]
    public void The_adapter_filters_a_claimant_to_their_own_claims_and_leaves_staff_unfiltered()
    {
        var claimant = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.Equal("claimantUserId = \"11111111-2222-3333-4444-555555555555\"", MeilisearchClaimSearch.BuildFilter(new ClaimSearchFilter("x", claimant, 20)));
        Assert.Null(MeilisearchClaimSearch.BuildFilter(new ClaimSearchFilter("x", null, 20)));
    }

    private async Task<ClaimDto> SubmitAsync(HttpClient client, string description)
    {
        HttpResponseMessage response = await ClaimsHarness.PostSubmitAsync(client, new
        {
            market = "SG",
            line = "Motor",
            policyNumber = ClaimsHarness.UniquePolicy(),
            incidentDate = _h.Yesterday().Iso(),
            incidentDescription = description,
            incidentLocation = "Orchard Road",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
    }
}
