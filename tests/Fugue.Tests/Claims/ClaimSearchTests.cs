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
/// configured (or an unreachable one) the answer is 503. Hardening (D-064 item 10): market names, codes, lines and their synonyms match, exact claim
/// and policy numbers match exactly (a mistyped one finds nothing), every word must match, hits say why they matched (highlights), facets count the
/// matches under the caller's scope, and the index settings are sent only when the engine's differ.
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
    private string _url = null!;

    public async Task InitializeAsync()
    {
        await _meili.StartAsync();
        _url = $"http://{_meili.Hostname}:{_meili.GetMappedPublicPort(7700)}";
        _factory = Engine(_url);
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
        // The fixture database and clock are shared by the whole collection, and other tests leave claims with later updated_at values
        // (ClaimReportsTests writes around 2030-03). The cursor follows the latest updated_at, so this test runs after all of them and
        // puts the clock back when it is done.
        DateTimeOffset original = db.Clock.Now;
        db.Clock.Now = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);
        try
        {
            HttpClient self = await ClientAsync(_factory, DevUsers.Self);
            HttpClient staff = await ClientAsync(_factory, DevUsers.Staff);
            // A policy or a place alone matches every other claim on it in the shared database (more than one page), so each query pairs the
            // field under test with a word only this claim carries; the engine ranks documents matching every word first.
            string rare = "Hailzorb" + Guid.NewGuid().ToString("N")[..6];
            ClaimDto claim = await SubmitAsync(self, rare + " dented the bonnet");
            await IndexAsync(_factory);

            Assert.Equal(claim.Number, (await NumbersAsync(self, claim.PolicyNumber + " " + rare)).FirstOrDefault());
            Assert.Equal(claim.Number, (await NumbersAsync(staff, rare)).FirstOrDefault());
            Assert.Equal(claim.Number, (await NumbersAsync(staff, "Orchard " + rare)).FirstOrDefault());

            db.Clock.Now = db.Clock.Now.AddMinutes(5); // a real change gets a later updated_at, which is what the cursor follows
            HttpResponseMessage pickup = await ClaimsHarness.SendAsync(staff, HttpMethod.Post, $"/api/v1/claims/{claim.Id}/pickup", claim.Version);
            Assert.Equal(HttpStatusCode.OK, pickup.StatusCode);
            // Do not assert how many rows this call sent: the app's own background ClaimIndexer runs on real timers (the test clock freezes
            // only "now") and may have indexed the change first. The outcome is what counts: the hit below shows the new status.
            await IndexAsync(_factory);

            HttpResponseMessage hits = await staff.GetAsync("/api/v1/claims/search?q=" + claim.Number);
            JsonElement hit = (await hits.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hits").EnumerateArray().First();
            Assert.Equal("UnderReview", hit.GetProperty("status").GetString());
            Assert.False(hit.TryGetProperty("claimantUserId", out _));
        }
        finally
        {
            db.Clock.Now = original;
        }
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

    [Fact]
    public async Task Market_codes_market_names_lines_and_their_synonyms_all_find_the_claim()
    {
        HttpClient self = await ClientAsync(_factory, DevUsers.Self);
        HttpClient staff = await ClientAsync(_factory, DevUsers.Staff);
        // Market and line alone match many claims in the shared database, so each query pairs them with a word only this claim carries.
        string rare = "Quillmorph" + Guid.NewGuid().ToString("N")[..6];
        ClaimDto home = await SubmitAsync(self, rare + " pipe burst in the kitchen", market: "MY", line: "Property");
        string rare2 = "Vexbramble" + Guid.NewGuid().ToString("N")[..6];
        ClaimDto car = await SubmitAsync(self, rare2 + " scraped at the lights");
        await IndexAsync(_factory);

        foreach (string q in new[] { "Malaysia", "malaysia", "MY", "Property", "home" })
        {
            Assert.Equal(home.Number, (await NumbersAsync(staff, q + " " + rare)).FirstOrDefault());
        }

        foreach (string q in new[] { "Singapore", "SG", "Motor", "car", "SG motor", "Singapore car" })
        {
            Assert.Equal(car.Number, (await NumbersAsync(staff, q + " " + rare2)).FirstOrDefault());
        }
    }

    [Fact]
    public async Task Index_settings_are_sent_once_a_restart_sends_none_and_drift_is_put_back()
    {
        await IndexAsync(_factory); // first start: the engine has the defaults, so one settings update
        Assert.Equal(1, await SettingsUpdatesAsync());

        await using (WebApplicationFactory<Program> restart = Engine(_url))
        {
            await IndexAsync(restart); // a new process compares and finds nothing to change
        }

        Assert.Equal(1, await SettingsUpdatesAsync());

        using HttpClient engine = EngineClient();
        HttpResponseMessage drift = await engine.PutAsJsonAsync("/indexes/claims/settings/synonyms", new Dictionary<string, string[]>());
        Assert.Equal(HttpStatusCode.Accepted, drift.StatusCode);
        await WaitForEngineAsync(engine);
        await using (WebApplicationFactory<Program> again = Engine(_url))
        {
            await IndexAsync(again); // someone changed a setting this app owns: the next start puts it back
        }

        Assert.Equal(3, await SettingsUpdatesAsync());
        JsonElement synonyms = await engine.GetFromJsonAsync<JsonElement>("/indexes/claims/settings/synonyms");
        Assert.Equal("singapore", synonyms.GetProperty("sg")[0].GetString());
        Assert.Equal("motor", synonyms.GetProperty("car")[0].GetString());
    }

    [Fact]
    public async Task A_checkpoint_from_an_older_document_shape_restarts_the_indexer_from_the_beginning()
    {
        await IndexAsync(_factory);
        MeilisearchClaimSearch search = _factory.Services.GetRequiredService<MeilisearchClaimSearch>();
        Assert.NotNull(await search.ReadCheckpointAsync(default));

        using HttpClient engine = EngineClient();
        var older = new { id = "claims", updatedAt = "2099-01-01T00:00:00.0000000+00:00", claimId = Guid.NewGuid().ToString() }; // no shape
        Assert.Equal(HttpStatusCode.Accepted, (await engine.PostAsJsonAsync("/indexes/claims_checkpoint/documents", new[] { older })).StatusCode);
        await WaitForEngineAsync(engine);

        Assert.Null(await search.ReadCheckpointAsync(default));
    }

    [Fact]
    public async Task An_exact_claim_or_policy_number_finds_only_that_claim_and_a_mistyped_one_does_not_find_it()
    {
        HttpClient self = await ClientAsync(_factory, DevUsers.Self);
        HttpClient staff = await ClientAsync(_factory, DevUsers.Staff);
        ClaimDto claim = await SubmitAsync(self, "Rear-ended at the lights");
        await IndexAsync(_factory);

        Assert.Equal([claim.Number], await NumbersAsync(staff, claim.Number));
        Assert.Equal([claim.Number], await NumbersAsync(staff, claim.Number.ToLowerInvariant()));
        Assert.Equal([claim.Number], await NumbersAsync(staff, claim.PolicyNumber));

        // One digit off: another claim may really carry that number (numbers are sequential), but never this one and never a near miss.
        string typo = claim.Number[..^1] + (claim.Number[^1] == '9' ? '8' : (char)(claim.Number[^1] + 1));
        string[] found = await NumbersAsync(staff, typo);
        Assert.DoesNotContain(claim.Number, found);
        Assert.All(found, n => Assert.Equal(typo, n));
        string policyTypo = claim.PolicyNumber[..^1] + (claim.PolicyNumber[^1] == 'A' ? 'B' : 'A');
        Assert.Empty(await NumbersAsync(staff, policyTypo));
    }

    [Fact]
    public async Task Every_word_must_match_so_the_exact_description_finds_the_claim_and_a_wrong_market_does_not()
    {
        HttpClient self = await ClientAsync(_factory, DevUsers.Self);
        HttpClient staff = await ClientAsync(_factory, DevUsers.Staff);
        ClaimDto exact = await SubmitAsync(self, "Fresh stack sink check"); // the owner's report: this description did not find its claim
        string rare = "Plinkwort" + Guid.NewGuid().ToString("N")[..6];
        ClaimDto claim = await SubmitAsync(self, rare + " hit a bollard");
        await IndexAsync(_factory);

        Assert.Contains(exact.Number, await NumbersAsync(staff, "Fresh stack sink check"));
        Assert.Equal([claim.Number], await NumbersAsync(staff, "Singapore " + rare));
        Assert.Empty(await NumbersAsync(staff, "Thailand " + rare));
        Assert.Empty(await NumbersAsync(staff, "home " + rare)); // the claim is Motor, home means Property
    }

    [Fact]
    public async Task Hits_say_why_they_matched_and_facets_count_the_matches_under_the_callers_scope()
    {
        HttpClient self = await ClientAsync(_factory, DevUsers.Self);
        HttpClient self2 = await ClientAsync(_factory, DevUsers.Self2);
        HttpClient staff = await ClientAsync(_factory, DevUsers.Staff);
        string rare = "Grommelfen" + Guid.NewGuid().ToString("N")[..6];
        ClaimDto claim = await SubmitAsync(self, "Reversed into a pillar, " + rare + " says the sensor failed");
        await IndexAsync(_factory);

        JsonElement body = await SearchAsync(staff, "SG car " + rare);
        JsonElement hit = Assert.Single(body.GetProperty("hits").EnumerateArray());
        JsonElement highlights = hit.GetProperty("highlights");
        Assert.Equal("<em>SG</em>", highlights.GetProperty("market").GetString());
        Assert.Equal("<em>Motor</em>", highlights.GetProperty("line").GetString()); // matched through the synonym car -> motor
        Assert.Contains("<em>" + rare + "</em>", highlights.GetProperty("incidentDescription").GetString(), StringComparison.Ordinal);
        Assert.False(highlights.TryGetProperty("policyNumber", out _)); // only matched fields appear
        Assert.Equal("<em>Singapore</em>", (await SearchAsync(staff, "Singapore " + rare)).GetProperty("hits")[0].GetProperty("highlights").GetProperty("marketName").GetString());

        JsonElement facets = body.GetProperty("facets");
        Assert.Equal(1, facets.GetProperty("market").GetProperty("SG").GetInt32());
        Assert.Equal(1, facets.GetProperty("line").GetProperty("Motor").GetInt32());
        Assert.Equal(1, facets.GetProperty("status").GetProperty("Submitted").GetInt32());

        // Another claimant's search for the same words: no hit and no count leaks through the facets.
        JsonElement theirs = await SearchAsync(self2, "SG car " + rare);
        Assert.Empty(theirs.GetProperty("hits").EnumerateArray());
        Assert.False(theirs.TryGetProperty("facets", out JsonElement f) && f.ValueKind == JsonValueKind.Object
            && f.EnumerateObject().Any(p => p.Value.EnumerateObject().Any()));
    }

    [Theory]
    [InlineData("SG motor", "SG motor")]
    [InlineData("CLM-SG-2026-900013", "\"CLM-SG-2026-900013\"")]
    [InlineData("clm-sg-2026-900013 motor", "\"clm-sg-2026-900013\" motor")]
    [InlineData("CLM-SG-2026-9000", "CLM-SG-2026-9000")] // a claim number still being typed stays a prefix
    [InlineData("POL-SG-100200", "\"POL-SG-100200\"")]
    [InlineData("900013", "\"900013\"")]
    [InlineData("2026 motor", "2026 motor")] // under 5 characters the engine allows no typo anyway
    [InlineData("  Orchard   Road ", "Orchard Road")]
    [InlineData("\"Fresh stack\" sink", "\"Fresh stack\" sink")] // the caller's own phrase is sent as typed
    public void Identifier_tokens_are_sent_as_exact_phrases(string typed, string sent) =>
        Assert.Equal(sent, MeilisearchClaimSearch.BuildQuery(typed));

    private static async Task<JsonElement> SearchAsync(HttpClient client, string q)
    {
        HttpResponseMessage response = await client.GetAsync("/api/v1/claims/search?q=" + Uri.EscapeDataString(q));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private HttpClient EngineClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(_url) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MasterKey);
        return client;
    }

    /// <summary>Settings updates the engine has recorded for the claims index (every one re-indexes the whole index).</summary>
    private async Task<int> SettingsUpdatesAsync()
    {
        using HttpClient engine = EngineClient();
        JsonElement tasks = await engine.GetFromJsonAsync<JsonElement>("/tasks?indexUids=claims&types=settingsUpdate");
        return tasks.GetProperty("total").GetInt32();
    }

    private static async Task WaitForEngineAsync(HttpClient engine)
    {
        for (int i = 0; i < 200; i++)
        {
            JsonElement pending = await engine.GetFromJsonAsync<JsonElement>("/tasks?statuses=enqueued,processing");
            if (pending.GetProperty("total").GetInt32() == 0)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Meilisearch tasks did not finish.");
    }

    private async Task<ClaimDto> SubmitAsync(HttpClient client, string description, string market = "SG", string line = "Motor")
    {
        HttpResponseMessage response = await ClaimsHarness.PostSubmitAsync(client, new
        {
            market,
            line,
            policyNumber = ClaimsHarness.UniquePolicy(),
            incidentDate = _h.Yesterday().Iso(),
            incidentDescription = description,
            incidentLocation = "Orchard Road",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
    }
}
