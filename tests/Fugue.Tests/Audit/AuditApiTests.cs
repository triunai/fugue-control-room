using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Customers.Models;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;

namespace Fugue.Tests.Audit;

/// <summary>GET /api/v1/audit: authorization, filters, keyset paging and the guarantee that masked values stay masked.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuditApiTests(PostgresFixture db)
{
    private static string NewCorrelation() => "audit-" + Guid.NewGuid().ToString("N");

    private static async Task<CustomerDto> CreateAsync(HttpClient client, string? correlation = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
        {
            Content = JsonContent.Create(new { name = "Grace Hopper", email = $"g{Guid.NewGuid():N}@example.test", phone = (string?)null }),
        };
        if (correlation is not null)
        {
            request.Headers.Add("X-Correlation-Id", correlation);
        }

        HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    private static async Task<JsonElement[]> PageAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    [Fact]
    public async Task Without_AUDIT_READ_the_endpoint_is_401_anonymous_and_403_for_staff_and_self_service()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await db.Anonymous().GetAsync("/api/v1/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await db.ClientAs(DevUsers.Staff).GetAsync("/api/v1/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await db.ClientAs(DevUsers.Self).GetAsync("/api/v1/audit")).StatusCode);
    }

    [Fact]
    public async Task A_grant_alone_opens_the_endpoint_to_a_user_who_is_not_an_admin()
    {
        (string email, _) = await db.NewUserAsync(overrides: [(Permissions.AuditRead, true, null)]);

        HttpResponseMessage response = await db.ClientAs(email).GetAsync("/api/v1/audit?limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Filtering_by_correlation_id_returns_exactly_the_rows_of_that_request()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        string correlation = NewCorrelation();
        CustomerDto customer = await CreateAsync(admin, correlation);
        await CreateAsync(admin); // another request that must not appear

        JsonElement[] rows = await PageAsync(await admin.GetAsync($"/api/v1/audit?correlationId={correlation}"));

        JsonElement row = Assert.Single(rows);
        Assert.Equal("customers.create", row.GetProperty("action").GetString());
        Assert.Equal(customer.Id.ToString(), row.GetProperty("entityId").GetString());
        Assert.Equal("success", row.GetProperty("outcome").GetString());
        Assert.Equal("CUSTOMERS_INSERT", row.GetProperty("permissionUsed").GetString());
        Assert.Equal("Grace Hopper", row.GetProperty("changes").GetProperty("name").GetProperty("new").GetString());
        Assert.Matches(@"^g\*\*\*@example\.test$", row.GetProperty("changes").GetProperty("email").GetProperty("new").GetString());
        Assert.Equal("CreateCustomerCommand", row.GetProperty("metadata").GetProperty("command").GetString());
    }

    [Fact]
    public async Task A_failure_row_exposes_its_error_type_message_and_duration_and_a_success_row_exposes_them_as_null()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        string ok = NewCorrelation();
        string failed = NewCorrelation();
        CustomerDto customer = await CreateAsync(admin, ok);
        var duplicate = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
        {
            Content = JsonContent.Create(new { name = "Dup", email = customer.Email, phone = (string?)null }),
        };
        duplicate.Headers.Add("X-Correlation-Id", failed);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(duplicate)).StatusCode);

        JsonElement success = Assert.Single(await PageAsync(await admin.GetAsync($"/api/v1/audit?correlationId={ok}")));
        JsonElement failure = Assert.Single(await PageAsync(await admin.GetAsync($"/api/v1/audit?correlationId={failed}")));

        Assert.Equal(JsonValueKind.Null, success.GetProperty("errorType").ValueKind);
        Assert.Equal(JsonValueKind.Null, success.GetProperty("errorMessage").ValueKind);
        Assert.Equal(JsonValueKind.Null, success.GetProperty("durationMs").ValueKind);
        Assert.Equal("Conflict", failure.GetProperty("errorType").GetString());
        Assert.Equal(JsonValueKind.Null, failure.GetProperty("errorMessage").ValueKind); // a business error stores no message text
        Assert.True(failure.GetProperty("durationMs").GetInt32() >= 0);
    }

    [Fact]
    public async Task Pages_are_keyset_ordered_newest_first_with_a_cursor_header_and_no_row_is_repeated()
    {
        (string email, Fugue.Domain.Ids.UserId actor) = await db.NewUserAsync([RoleCodes.Admin]);
        HttpClient admin = db.ClientAs(email);
        var created = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            created.Add((await CreateAsync(admin)).Id.ToString());
        }

        HttpResponseMessage first = await admin.GetAsync($"/api/v1/audit?actorUserId={actor}&action=customers.create&limit=2");
        JsonElement[] page1 = await PageAsync(first);
        string cursor = first.Headers.GetValues("X-Next-Cursor").Single();
        Assert.Contains("rel=\"next\"", first.Headers.GetValues("Link").Single());
        JsonElement[] page2 = await PageAsync(await admin.GetAsync($"/api/v1/audit?actorUserId={actor}&action=customers.create&limit=2&cursor={Uri.EscapeDataString(cursor)}"));
        HttpResponseMessage last = await admin.GetAsync($"/api/v1/audit?actorUserId={actor}&action=customers.create&limit=2&cursor={Uri.EscapeDataString(cursor)}");

        Assert.Equal(2, page1.Length);
        Assert.Single(page2);
        Assert.False(last.Headers.Contains("X-Next-Cursor")); // the last page has no cursor
        string[] ids = page1.Concat(page2).Select(r => r.GetProperty("entityId").GetString()!).ToArray();
        Assert.Equal(created.AsEnumerable().Reverse().ToArray(), ids); // newest first, each exactly once
    }

    [Fact]
    public async Task Filters_by_entity_action_and_time_window_narrow_the_result()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        CustomerDto customer = await CreateAsync(admin);

        JsonElement[] byEntity = await PageAsync(await admin.GetAsync($"/api/v1/audit?entityType=Customer&entityId={customer.Id}"));
        JsonElement[] wrongAction = await PageAsync(await admin.GetAsync($"/api/v1/audit?entityType=Customer&entityId={customer.Id}&action=customers.delete"));
        JsonElement[] future = await PageAsync(await admin.GetAsync($"/api/v1/audit?entityId={customer.Id}&from=2099-01-01T00:00:00Z"));
        JsonElement[] window = await PageAsync(await admin.GetAsync($"/api/v1/audit?entityId={customer.Id}&from=2000-01-01T00:00:00Z&to=2099-01-01T00:00:00Z"));

        Assert.Single(byEntity);
        Assert.Empty(wrongAction);
        Assert.Empty(future);
        Assert.Single(window);
    }

    [Fact]
    public async Task Filter_values_are_data_not_SQL_and_bad_input_is_a_400()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);

        JsonElement[] injection = await PageAsync(await admin.GetAsync("/api/v1/audit?entityType=" + Uri.EscapeDataString("x' OR '1'='1") + "&action=" + Uri.EscapeDataString("a'; DROP TABLE audit_log;--")));

        Assert.Empty(injection);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/audit?limit=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/audit?limit=1000")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/audit?cursor=not-a-cursor")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/audit?from=2030-01-02T00:00:00Z&to=2030-01-01T00:00:00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/audit?actorUserId=nope")).StatusCode);
        Assert.True((await admin.GetAsync("/api/v1/audit?limit=1")).IsSuccessStatusCode); // the table survived
    }

    [Fact]
    public async Task The_response_never_un_masks_a_secret()
    {
        (string email, Fugue.Domain.Ids.UserId id) = await db.NewUserAsync([RoleCodes.SelfService]);

        string body = await db.ClientAs(DevUsers.Admin).GetStringAsync($"/api/v1/audit?entityType=User&entityId={id}");

        using JsonDocument json = JsonDocument.Parse(body);
        Assert.Equal("***", json.RootElement[0].GetProperty("changes").GetProperty("passwordHash").GetProperty("new").GetString());
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task There_is_no_way_to_write_through_the_API()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.PostAsJsonAsync("/api/v1/audit", new { action = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync("/api/v1/audit")).StatusCode);
    }
}
