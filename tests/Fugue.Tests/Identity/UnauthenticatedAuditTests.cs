using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Fugue.Domain.Identity;
using Fugue.Tests.Audit;
using Fugue.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Fugue.Tests.Identity;

/// <summary>
/// W-018: a 401 challenge leaves an <c>unauthenticated</c>/<c>denied</c> access_audit row with the reason, bounded per client IP, and the
/// response itself does not change. Each test uses its own host so the per-IP window is its own (the shared host sees many 401s).
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class UnauthenticatedAuditTests(PostgresFixture db)
{
    private sealed record Row(string Event, string Outcome, string? Reason, Guid? UserId, string? EmailHash);

    private WebApplicationFactory<Program> HostWithCap(int cap) =>
        db.Factory.WithWebHostBuilder(b => b.UseSetting("Audit:UnauthenticatedRowsPerIpPerMinute", cap.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static string NewCorrelation() => "unauth-" + Guid.NewGuid().ToString("N");

    private static HttpRequestMessage Get(string correlation, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers?limit=1");
        request.Headers.Add("X-Correlation-Id", correlation);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return request;
    }

    private async Task<List<Row>> RowsAsync(string correlation)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT event, outcome, reason_code, user_id, email_hash FROM access_audit WHERE correlation_id = @c ORDER BY occurred_at, id", connection);
        command.Parameters.AddWithValue("c", correlation);
        var rows = new List<Row>();
        await using NpgsqlDataReader r = await command.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new Row(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetString(4)));
        }

        return rows;
    }

    [Fact]
    public async Task A_request_without_a_token_records_unauthenticated_denied_missing_token()
    {
        await using WebApplicationFactory<Program> host = HostWithCap(1000);
        string correlation = NewCorrelation();

        HttpResponseMessage response = await host.CreateClient().SendAsync(Get(correlation));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Row row = Assert.Single(await RowsAsync(correlation));
        Assert.Equal(("unauthenticated", "denied", "missing_token", null, null), (row.Event, row.Outcome, row.Reason, row.UserId, row.EmailHash));
    }

    [Fact]
    public async Task A_garbage_token_records_invalid_token_and_the_token_is_not_stored()
    {
        await using WebApplicationFactory<Program> host = HostWithCap(1000);
        string correlation = NewCorrelation();
        string token = "garbage-" + Guid.NewGuid().ToString("N");

        HttpResponseMessage response = await host.CreateClient().SendAsync(Get(correlation, token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_token", Assert.Single(await RowsAsync(correlation)).Reason);
        Assert.Equal(0L, await CountAccessAuditContainingAsync(token));
    }

    [Fact]
    public async Task An_expired_token_records_expired()
    {
        await using WebApplicationFactory<Program> host = HostWithCap(1000);
        string correlation = NewCorrelation();
        (_, Fugue.Domain.Ids.UserId user) = await db.NewUserAsync([RoleCodes.Admin]);
        string token = TestTokens.Make(db.SigningKey, user.Value, db.Clock.GetUtcNow(), lifetime: TimeSpan.FromHours(-1));

        HttpResponseMessage response = await host.CreateClient().SendAsync(Get(correlation, token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("expired", Assert.Single(await RowsAsync(correlation)).Reason);
    }

    [Fact]
    public async Task A_token_with_a_bad_signature_is_invalid_token_not_expired()
    {
        await using WebApplicationFactory<Program> host = HostWithCap(1000);
        string correlation = NewCorrelation();
        string token = TestTokens.Make(db.SigningKey, Guid.NewGuid(), db.Clock.GetUtcNow(), keyBytes: System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        await host.CreateClient().SendAsync(Get(correlation, token));

        Assert.Equal("invalid_token", Assert.Single(await RowsAsync(correlation)).Reason);
    }

    [Fact]
    public async Task Rows_stop_at_the_per_IP_cap_while_every_request_is_still_answered_401_and_the_excess_is_counted()
    {
        await using WebApplicationFactory<Program> host = HostWithCap(3);
        HttpClient client = host.CreateClient();
        string[] correlations = Enumerable.Range(0, 7).Select(_ => NewCorrelation()).ToArray();
        using var capture = new AuditMeterCapture();

        var statuses = new List<HttpStatusCode>();
        foreach (string correlation in correlations)
        {
            statuses.Add((await client.SendAsync(Get(correlation))).StatusCode);
        }

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        long written = 0;
        foreach (string correlation in correlations)
        {
            written += (await RowsAsync(correlation)).Count;
        }

        Assert.Equal(3, written);
        Assert.True(capture.Count("access_audit", "suppressed") >= 4); // other hosts may add to the process-wide meter; ours added 4
    }

    [Fact]
    public async Task A_cap_of_zero_records_nothing_and_still_answers_401()
    {
        await using WebApplicationFactory<Program> host = HostWithCap(0);
        string correlation = NewCorrelation();
        using var capture = new AuditMeterCapture();

        HttpResponseMessage response = await host.CreateClient().SendAsync(Get(correlation));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await RowsAsync(correlation));
        Assert.True(capture.Count("access_audit", "suppressed") >= 1);
    }

    [Fact]
    public async Task The_401_response_is_the_same_whether_or_not_a_row_is_recorded()
    {
        await using WebApplicationFactory<Program> recording = HostWithCap(1000);
        await using WebApplicationFactory<Program> silent = HostWithCap(0);
        string correlation = NewCorrelation();

        HttpResponseMessage withRow = await recording.CreateClient().SendAsync(Get(correlation));
        HttpResponseMessage without = await silent.CreateClient().SendAsync(Get(correlation));

        Assert.Equal(without.StatusCode, withRow.StatusCode);
        Assert.Equal(Describe(without), Describe(withRow));
        Assert.Equal(await NormalisedBodyAsync(without), await NormalisedBodyAsync(withRow));
        Assert.Equal("Bearer", withRow.Headers.WwwAuthenticate.Single().Scheme);
    }

    private static string Describe(HttpResponseMessage response) =>
        string.Join("\n", response.Headers.Concat(response.Content.Headers)
            .Where(h => !string.Equals(h.Key, "Date", StringComparison.OrdinalIgnoreCase))
            .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));

    private static async Task<string> NormalisedBodyAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // The trace id differs per request by design; everything else in the problem must match.
        return JsonSerializer.Serialize(body.RootElement.EnumerateObject().Where(p => p.Name != "traceId").OrderBy(p => p.Name).ToDictionary(p => p.Name, p => p.Value.ToString()));
    }

    private async Task<long> CountAccessAuditContainingAsync(string fragment)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT count(*) FROM access_audit a WHERE a::text ILIKE @f", connection);
        command.Parameters.AddWithValue("f", "%" + fragment + "%");
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
