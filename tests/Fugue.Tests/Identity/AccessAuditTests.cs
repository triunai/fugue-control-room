using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fugue.Application.Features.Auth;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;
using Npgsql;

namespace Fugue.Tests.Identity;

/// <summary>access_audit: authentication and authorization events, with the correlation id, and without secrets.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AccessAuditTests(PostgresFixture db)
{
    private sealed record Row(string Event, string Outcome, string? Reason, Guid? UserId, string? EmailHash, string? Correlation);

    private async Task<List<Row>> Rows(string correlationId)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT event, outcome, reason_code, user_id, email_hash, correlation_id FROM access_audit WHERE correlation_id = @c ORDER BY occurred_at, id", connection);
        command.Parameters.AddWithValue("c", correlationId);
        var rows = new List<Row>();
        await using NpgsqlDataReader r = await command.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new Row(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5)));
        }

        return rows;
    }

    private static HttpRequestMessage Post(string path, object body, string correlation, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Correlation-Id", correlation);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return request;
    }

    [Fact]
    public async Task A_failed_login_records_the_real_reason_a_hashed_email_and_the_correlation_id_but_no_secret()
    {
        string correlation = "audit-" + Guid.NewGuid().ToString("N");
        string email = $"Ghost-{Guid.NewGuid():N}@Example.Test";

        HttpResponseMessage response = await db.Anonymous().SendAsync(Post("/api/v1/auth/token", new { email, password = "Wrong-Passw0rd!x" }, correlation));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Row row = Assert.Single(await Rows(correlation));
        Assert.Equal(("login", "failure", "unknown_user"), (row.Event, row.Outcome, row.Reason));
        Assert.Null(row.UserId);
        Assert.Matches("^[0-9a-f]{64}$", row.EmailHash);

        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand scan = new("SELECT count(*) FROM access_audit a WHERE a::text ILIKE '%wrong-passw0rd%' OR a::text ILIKE @e", connection);
        scan.Parameters.AddWithValue("e", "%" + email.Split('@')[0] + "%");
        Assert.Equal(0L, await scan.ExecuteScalarAsync()); // neither the password nor the address is in any column
    }

    [Fact]
    public async Task A_wrong_password_for_a_real_user_is_audited_with_the_user_id()
    {
        string correlation = "audit-" + Guid.NewGuid().ToString("N");

        await db.Anonymous().SendAsync(Post("/api/v1/auth/token", new { email = DevUsers.Staff, password = "nope-nope-nope" }, correlation));

        Row row = Assert.Single(await Rows(correlation));
        Assert.Equal("bad_password", row.Reason);
        Assert.NotNull(row.UserId);
    }

    [Fact]
    public async Task Login_success_refresh_and_refresh_reuse_are_audited()
    {
        string c1 = "audit-" + Guid.NewGuid().ToString("N");
        HttpResponseMessage login = await db.Anonymous().SendAsync(Post("/api/v1/auth/token", new { email = DevUsers.Staff, password = TestAuth.Password }, c1));
        TokenDto token = (await login.Content.ReadFromJsonAsync<TokenDto>())!;
        Assert.Equal(("login", "success"), (Assert.Single(await Rows(c1)).Event, Assert.Single(await Rows(c1)).Outcome));

        string c2 = "audit-" + Guid.NewGuid().ToString("N");
        await db.Anonymous().SendAsync(Post("/api/v1/auth/refresh", new { refreshToken = token.RefreshToken }, c2));
        Assert.Equal("success", Assert.Single(await Rows(c2)).Outcome);

        string c3 = "audit-" + Guid.NewGuid().ToString("N");
        await db.Anonymous().SendAsync(Post("/api/v1/auth/refresh", new { refreshToken = token.RefreshToken }, c3));
        Row reuse = Assert.Single(await Rows(c3));
        Assert.Equal(("refresh_reuse", "failure", "reused"), (reuse.Event, reuse.Outcome, reuse.Reason));
    }

    [Fact]
    public async Task A_403_denial_is_audited_with_the_missing_permission_but_the_client_is_not_told_it()
    {
        string correlation = "audit-" + Guid.NewGuid().ToString("N");
        using HttpClient self = db.ClientAs(DevUsers.Self);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/customers/{Guid.NewGuid()}");
        request.Headers.Add("X-Correlation-Id", correlation);

        HttpResponseMessage response = await self.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("CUSTOMERS_DELETE", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Row row = Assert.Single(await Rows(correlation));
        Assert.Equal(("forbidden", "denied", "CUSTOMERS_DELETE"), (row.Event, row.Outcome, row.Reason));
        Assert.NotNull(row.UserId);
    }

    [Fact]
    public async Task A_request_to_a_disabled_module_is_audited()
    {
        string correlation = "audit-" + Guid.NewGuid().ToString("N");
        using var factory = db.Factory.WithWebHostBuilder(b => b.UseSetting("Modules:CUSTOMERS:Enabled", "false"));
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await db.TokenAsync(DevUsers.Admin));
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlation);

        HttpResponseMessage response = await client.GetAsync("/api/v1/customers");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Row row = Assert.Single(await Rows(correlation));
        Assert.Equal(("module_disabled", "denied", "CUSTOMERS"), (row.Event, row.Outcome, row.Reason));
    }
}
