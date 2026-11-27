using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Fugue.Application.Features.Audit;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Search;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Tests.Audit;

/// <summary>
/// GET /api/v1/audit/search against a real Meilisearch (same pinned image as docker-compose.yml): the indexer picks rows up, search finds them
/// by actor and by error code, filters apply, AUDIT_READ is enforced, and with no engine configured (or an unreachable one) the answer is 503.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuditSearchTests(PostgresFixture db) : IAsyncLifetime
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

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        await _meili.StartAsync();
        _factory = Engine($"http://{_meili.Hostname}:{_meili.GetMappedPublicPort(7700)}");
        _admin = await ClientAsync(_factory, DevUsers.Admin);
    }

    public async Task DisposeAsync()
    {
        _admin.Dispose();
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
        factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<AuditIndexer>().Single().IndexOnceAsync(default);

    private static async Task<(string Id, string Email)> CreateCustomerAsync(HttpClient client, string? email = null)
    {
        email ??= $"s{Guid.NewGuid():N}@example.test";
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/customers", new { name = "Search Subject", email, phone = (string?)null });
        return response.StatusCode == HttpStatusCode.Created
            ? ((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!, email)
            : ("", email);
    }

    private static async Task<JsonElement> SearchAsync(HttpClient client, string query)
    {
        HttpResponseMessage response = await client.GetAsync("/api/v1/audit/search?" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Without_a_search_engine_configured_the_endpoint_answers_503_search_unavailable()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);

        HttpResponseMessage response = await admin.GetAsync("/api/v1/audit/search?q=anything");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("search.unavailable", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unreachable_engine_answers_503_search_unavailable_too()
    {
        using WebApplicationFactory<Program> broken = Engine("http://127.0.0.1:1");
        HttpClient admin = await ClientAsync(broken, DevUsers.Admin);

        HttpResponseMessage response = await admin.GetAsync("/api/v1/audit/search?q=anything");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Without_AUDIT_READ_the_endpoint_is_401_anonymous_and_403_for_staff_even_when_search_is_on()
    {
        HttpClient staff = await ClientAsync(_factory, DevUsers.Staff);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/v1/audit/search")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/audit/search")).StatusCode);
    }

    [Fact]
    public async Task The_indexer_picks_up_new_rows_and_search_finds_them_by_entity_and_by_actor()
    {
        (string id, _) = await CreateCustomerAsync(_admin);
        await IndexAsync(_factory);

        JsonElement result = await SearchAsync(_admin, $"q={id}");

        JsonElement hit = result.GetProperty("hits").EnumerateArray().First(h => h.GetProperty("entityId").GetString() == id);
        Assert.Equal("customers.create", hit.GetProperty("action").GetString());
        Assert.Equal("Dev Admin", hit.GetProperty("actorDisplay").GetString());
        Assert.True(result.GetProperty("estimatedTotalHits").GetInt32() >= 1);
        Assert.Contains("<em>", hit.GetProperty("highlights").GetProperty("entityId").GetString());

        // Rows matching both words rank first; when fewer than a page of them are indexed, partial matches ("Dev Self-Service One") fill the rest.
        JsonElement byActor = await SearchAsync(_admin, "q=Dev%20Admin&limit=5");
        bool[] isAdmin = [.. byActor.GetProperty("hits").EnumerateArray().Select(h => h.GetProperty("actorDisplay").GetString() == "Dev Admin")];
        Assert.NotEmpty(isAdmin);
        Assert.True(isAdmin[0]);
        Assert.Equal(isAdmin.OrderByDescending(a => a), isAdmin); // no partial match outranks a full one
    }

    [Fact]
    public async Task Search_finds_a_failure_row_by_its_error_code_and_the_outcome_filter_narrows_to_it()
    {
        string email = $"dup{Guid.NewGuid():N}@example.test";
        await CreateCustomerAsync(_admin, email);
        await CreateCustomerAsync(_admin, email); // 409 customer.email_taken
        await IndexAsync(_factory);

        JsonElement result = await SearchAsync(_admin, "q=customer.email_taken&outcome=failure&limit=100");

        JsonElement[] hits = [.. result.GetProperty("hits").EnumerateArray()];
        Assert.Contains(hits, h => h.GetProperty("errorCode").GetString() == "customer.email_taken");
        Assert.All(hits, h => Assert.Equal("failure", h.GetProperty("outcome").GetString()));
    }

    [Fact]
    public async Task Filters_apply_entity_type_action_and_date_range()
    {
        (string id, _) = await CreateCustomerAsync(_admin);
        await IndexAsync(_factory);

        JsonElement match = await SearchAsync(_admin, $"q={id}&entityType=Customer&action=customers.create");
        JsonElement wrongType = await SearchAsync(_admin, $"q={id}&entityType=Claim");
        JsonElement before = await SearchAsync(_admin, $"q={id}&to=2000-01-01T00:00:00Z");
        JsonElement after = await SearchAsync(_admin, $"q={id}&from=2000-01-01T00:00:00Z");

        Assert.Contains(match.GetProperty("hits").EnumerateArray(), h => h.GetProperty("entityId").GetString() == id);
        Assert.DoesNotContain(wrongType.GetProperty("hits").EnumerateArray(), h => h.GetProperty("entityId").GetString() == id);
        Assert.DoesNotContain(before.GetProperty("hits").EnumerateArray(), h => h.GetProperty("entityId").GetString() == id);
        Assert.Contains(after.GetProperty("hits").EnumerateArray(), h => h.GetProperty("entityId").GetString() == id);
    }

    [Fact]
    public async Task Results_are_newest_first_and_limit_is_validated()
    {
        await CreateCustomerAsync(_admin);
        await CreateCustomerAsync(_admin);
        await IndexAsync(_factory);

        JsonElement result = await SearchAsync(_admin, "action=customers.create&limit=5");

        DateTimeOffset[] times = [.. result.GetProperty("hits").EnumerateArray().Select(h => h.GetProperty("occurredAt").GetDateTimeOffset())];
        Assert.Equal(times.OrderByDescending(t => t), times);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.GetAsync("/api/v1/audit/search?limit=0")).StatusCode);
    }

    [Fact]
    public async Task The_indexer_is_idempotent_and_a_restarted_host_resumes_from_the_checkpoint_in_the_engine()
    {
        await CreateCustomerAsync(_admin);
        await IndexAsync(_factory);

        int again = await IndexAsync(_factory);
        using WebApplicationFactory<Program> restarted = Engine($"http://{_meili.Hostname}:{_meili.GetMappedPublicPort(7700)}");
        _ = restarted.Server; // start the second host
        int afterRestart = await IndexAsync(restarted);

        Assert.Equal(0, again);
        Assert.Equal(0, afterRestart);

        (string id, _) = await CreateCustomerAsync(_admin);
        Assert.True(await IndexAsync(restarted) >= 1);
        Assert.Contains((await SearchAsync(_admin, $"q={id}")).GetProperty("hits").EnumerateArray(), h => h.GetProperty("entityId").GetString() == id);
    }

    [Fact]
    public void The_search_hit_type_exposes_no_changes_or_metadata_so_the_index_stays_what_the_row_stored()
    {
        string[] names = [.. typeof(AuditSearchHitDto).GetProperties().Select(p => p.Name)];

        Assert.DoesNotContain("Changes", names);
        Assert.DoesNotContain("Metadata", names);
    }
}
