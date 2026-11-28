using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Customers.Models;
using Fugue.Domain.Customers;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fugue.Tests.Persistence;

/// <summary>
/// End to end through HTTP against real Postgres. Tests whose assertions need <c>MapStatus</c> (any non-2xx
/// produced by a handler failure) are marked RED in their summary: they stay red until the owner implements
/// <c>ApiControllerBase.MapStatus</c>. The same failure logic is also pinned without HTTP in
/// <see cref="CustomerSeamTests"/>, which is green.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CustomersApiTests(PostgresFixture db)
{
    private readonly HttpClient _client = db.ClientAs(DevUsers.Admin);

    [Fact]
    public async Task Lifecycle_create_get_update_suspend_reactivate_delete()
    {
        string email = UniqueEmail();
        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/customers", new { name = "Ada Lovelace", email = email.ToUpperInvariant(), phone = "+14155550100" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        CustomerDto customer = (await created.Content.ReadFromJsonAsync<CustomerDto>())!;
        Assert.Equal(email, customer.Email); // lower-cased
        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.Equal($"/api/v1/customers/{customer.Id}", created.Headers.Location!.AbsolutePath);
        string createdTag = created.Headers.ETag!.Tag;
        Assert.Equal($"\"{customer.Version}\"", createdTag);

        HttpResponseMessage fetched = await _client.GetAsync($"/api/v1/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(createdTag, fetched.Headers.ETag!.Tag);

        HttpResponseMessage updated = await Send(HttpMethod.Put, customer.Id, createdTag, new { name = "Ada King", email, phone = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        string updatedTag = updated.Headers.ETag!.Tag; // 204 still carries the new ETag so the client can chain If-Match
        Assert.NotEqual(createdTag, updatedTag);
        Assert.Equal("Ada King", (await _client.GetFromJsonAsync<CustomerDto>($"/api/v1/customers/{customer.Id}"))!.Name);

        HttpResponseMessage suspended = await Send(HttpMethod.Post, $"{customer.Id}/suspend", null, null);
        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        Assert.Equal(CustomerStatus.Suspended, (await _client.GetFromJsonAsync<CustomerDto>($"/api/v1/customers/{customer.Id}"))!.Status);

        HttpResponseMessage reactivated = await Send(HttpMethod.Post, $"{customer.Id}/reactivate", null, null);
        Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        Assert.Equal(CustomerStatus.Active, (await _client.GetFromJsonAsync<CustomerDto>($"/api/v1/customers/{customer.Id}"))!.Status);

        HttpResponseMessage deleted = await Send(HttpMethod.Delete, customer.Id, reactivated.Headers.ETag!.Tag, null);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0L, await Scalar<long>("SELECT count(*) FROM customers WHERE email = @e", ("e", email)));
    }

    /// <summary>stale If-Match is 412.</summary>
    [Fact]
    public async Task Stale_if_match_is_412_version_mismatch()
    {
        CustomerDto customer = await Create();
        string oldTag = $"\"{customer.Version}\"";
        await Send(HttpMethod.Put, customer.Id, oldTag, new { name = "Changed", email = customer.Email, phone = (string?)null });

        HttpResponseMessage stale = await Send(HttpMethod.Put, customer.Id, oldTag, new { name = "Again", email = customer.Email, phone = (string?)null });

        await AssertProblem(stale, HttpStatusCode.PreconditionFailed, "customer.version_mismatch");
    }

    /// <summary>PUT and DELETE without If-Match are 428.</summary>
    [Fact]
    public async Task Missing_if_match_is_428()
    {
        CustomerDto customer = await Create();

        HttpResponseMessage put = await Send(HttpMethod.Put, customer.Id, null, new { name = "X", email = customer.Email, phone = (string?)null });
        HttpResponseMessage delete = await Send(HttpMethod.Delete, customer.Id, null, null);

        await AssertProblem(put, HttpStatusCode.PreconditionRequired, "customer.if_match_required");
        await AssertProblem(delete, HttpStatusCode.PreconditionRequired, "customer.if_match_required");
    }

    /// <summary>suspending a suspended customer is 409.</summary>
    [Fact]
    public async Task Suspend_twice_is_409_invalid_transition()
    {
        CustomerDto customer = await Create();
        await Send(HttpMethod.Post, $"{customer.Id}/suspend", null, null);

        HttpResponseMessage again = await Send(HttpMethod.Post, $"{customer.Id}/suspend", null, null);

        await AssertProblem(again, HttpStatusCode.Conflict, "customer.invalid_transition");
    }

    /// <summary>a duplicate email is 409.</summary>
    [Fact]
    public async Task Duplicate_email_is_409()
    {
        CustomerDto first = await Create();

        HttpResponseMessage duplicate = await _client.PostAsJsonAsync("/api/v1/customers", new { name = "Other", email = first.Email });

        await AssertProblem(duplicate, HttpStatusCode.Conflict, "customer.email_taken");
    }

    /// <summary>an unknown id is 404.</summary>
    [Fact]
    public async Task Unknown_customer_is_404()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");

        await AssertProblem(response, HttpStatusCode.NotFound, "customer.not_found");
    }

    [Fact]
    public async Task Unknown_route_for_a_signed_in_caller_is_404_problem_json()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_malformed_id_is_a_framework_400_problem_json()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/customers/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation.failed", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Keyset_paging_walks_two_pages_via_link_and_next_cursor()
    {
        string token = "pg" + Guid.NewGuid().ToString("N")[..10];
        var created = new List<CustomerDto>();
        for (int i = 0; i < 5; i++)
        {
            db.Clock.Now = db.Clock.Now.AddSeconds(1); // distinct created_at so order is the creation order
            created.Add(await Create($"{token}-{i}"));
        }

        HttpResponseMessage page1 = await _client.GetAsync($"/api/v1/customers?limit=2&search={token}");
        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        CustomerDto[] first = (await page1.Content.ReadFromJsonAsync<CustomerDto[]>())!;
        Assert.Equal(created.Take(2).Select(c => c.Id), first.Select(c => c.Id));
        string cursor = page1.Headers.GetValues("X-Next-Cursor").Single();
        string link = page1.Headers.GetValues("Link").Single();
        Assert.Contains(cursor, link, StringComparison.Ordinal);
        Assert.EndsWith("rel=\"next\"", link, StringComparison.Ordinal);

        string nextUrl = link[1..link.IndexOf('>', StringComparison.Ordinal)];
        HttpResponseMessage page2 = await _client.GetAsync(nextUrl);
        CustomerDto[] second = (await page2.Content.ReadFromJsonAsync<CustomerDto[]>())!;
        Assert.Equal(created.Skip(2).Take(2).Select(c => c.Id), second.Select(c => c.Id));

        HttpResponseMessage page3 = await _client.GetAsync($"/api/v1/customers?limit=2&search={token}&cursor={page2.Headers.GetValues("X-Next-Cursor").Single()}");
        CustomerDto[] third = (await page3.Content.ReadFromJsonAsync<CustomerDto[]>())!;
        Assert.Equal([created[4].Id], third.Select(c => c.Id));
        Assert.False(page3.Headers.Contains("X-Next-Cursor"));
    }

    [Fact]
    public async Task Search_escapes_like_wildcards()
    {
        string token = "wc" + Guid.NewGuid().ToString("N")[..8];
        await Create($"{token}_a"); // underscore is a LIKE wildcard when unescaped

        CustomerDto[] literal = (await _client.GetFromJsonAsync<CustomerDto[]>($"/api/v1/customers?search={token}_a"))!;
        CustomerDto[] wildcard = (await _client.GetFromJsonAsync<CustomerDto[]>($"/api/v1/customers?search={token}%25"))!;

        Assert.Single(literal);
        Assert.Empty(wildcard); // a literal '%' matches nothing
    }

    /// <summary>Both write paths stamp audit fields: Dapper on insert, the EF interceptor on update. The actor is the signed-in user.</summary>
    [Fact]
    public async Task Audit_fields_are_stamped_by_the_dapper_insert_and_the_ef_update()
    {
        (string creatorEmail, UserId creator) = await db.NewUserAsync([RoleCodes.Staff]);
        (string editorEmail, UserId editor) = await db.NewUserAsync([RoleCodes.Staff]);
        using HttpClient creatorClient = db.ClientAs(creatorEmail);
        using HttpClient editorClient = db.ClientAs(editorEmail);
        var createdAt = new DateTimeOffset(2031, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var updatedAt = createdAt.AddHours(2);

        db.Clock.Now = createdAt;
        HttpResponseMessage created = await creatorClient.PostAsJsonAsync("/api/v1/customers", new { name = "Audit", email = UniqueEmail() });
        CustomerDto customer = (await created.Content.ReadFromJsonAsync<CustomerDto>())!;
        Assert.Equal(creator.Value.ToString(), customer.CreatedBy);
        Assert.Equal(createdAt, customer.CreatedAt);

        db.Clock.Now = updatedAt;
        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/customers/{customer.Id}") { Content = JsonContent.Create(new { name = "Renamed", email = customer.Email, phone = (string?)null }) };
        put.Headers.TryAddWithoutValidation("If-Match", $"\"{customer.Version}\"");
        Assert.Equal(HttpStatusCode.NoContent, (await editorClient.SendAsync(put)).StatusCode);
        db.Clock.Now = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT created_at, created_by, updated_at, updated_by FROM customers WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", customer.Id.Value);
        await using NpgsqlDataReader row = await command.ExecuteReaderAsync();
        Assert.True(await row.ReadAsync());
        Assert.Equal(createdAt, row.GetFieldValue<DateTimeOffset>(0));
        Assert.Equal(creator.Value.ToString(), row.GetString(1));
        Assert.Equal(updatedAt, row.GetFieldValue<DateTimeOffset>(2));
        Assert.Equal(editor.Value.ToString(), row.GetString(3));
    }

    /// <summary>With no HTTP user (seeder, background work) the actor is "system".</summary>
    [Fact]
    public async Task Without_an_http_user_the_actor_is_system()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        CustomerDto customer = (await scope.ServiceProvider.GetRequiredService<Mediator.ISender>()
            .Send(new Fugue.Application.Features.Customers.Commands.CreateCustomerCommand("Sys", UniqueEmail(), null))).Value;

        Assert.Equal("system", customer.CreatedBy);
        Assert.Equal("system", customer.UpdatedBy);
    }

    private async Task<CustomerDto> Create(string? name = null)
    {
        string unique = UniqueEmail();
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/customers", new { name = name ?? "Test Customer", email = unique });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, object idOrPath, string? ifMatch, object? body)
    {
        using var request = new HttpRequestMessage(method, $"/api/v1/customers/{idOrPath}");
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(status, response.StatusCode);
    }

    private async Task<T> Scalar<T>(string sql, (string Name, object Value) parameter)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.test";
}
