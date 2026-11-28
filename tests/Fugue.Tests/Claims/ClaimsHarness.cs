using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Claims.Models;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;
using Npgsql;

namespace Fugue.Tests.Claims;

/// <summary>Shared plumbing for the claims HTTP tests: submit, send a mutating call with If-Match, read the problem code, count rows.</summary>
internal sealed class ClaimsHarness(PostgresFixture db)
{
    public PostgresFixture Db { get; } = db;

    public HttpClient Claimant(string? email = null) => Db.ClientAs(email ?? DevUsers.Self);

    public HttpClient Officer(string? email = null) => Db.ClientAs(email ?? DevUsers.Staff);

    public HttpClient Admin() => Db.ClientAs(DevUsers.Admin);

    /// <summary>A fresh user holding a role, as a client.</summary>
    public async Task<(string Email, HttpClient Client)> NewUserClientAsync(string role)
    {
        (string email, _) = await Db.NewUserAsync([role]);
        return (email, Db.ClientAs(email));
    }

    /// <summary>A date safely inside the allowed window for any market, relative to the fixture clock.</summary>
    public DateOnly Yesterday() => DateOnly.FromDateTime(Db.Clock.GetUtcNow().UtcDateTime).AddDays(-1);

    public static string UniquePolicy() => "POL-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    public async Task<ClaimDto> SubmitAsync(HttpClient client, Market market = Market.SG, LineOfBusiness line = LineOfBusiness.Motor, string? policy = null)
    {
        HttpResponseMessage response = await PostSubmitAsync(client, new
        {
            market = market.ToString(),
            line = line.ToString(),
            policyNumber = policy ?? UniquePolicy(),
            incidentDate = Yesterday().Iso(),
            incidentDescription = "Rear-ended at the lights.",
            incidentLocation = "Orchard Road",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
    }

    /// <summary>POSTs a claim submit with a fresh <c>Idempotency-Key</c> (the key is required on this operation).</summary>
    public static async Task<HttpResponseMessage> PostSubmitAsync(HttpClient client, HttpContent content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/claims") { Content = content };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PostSubmitAsync(HttpClient client, object body) => PostSubmitAsync(client, JsonContent.Create(body));

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, long? version, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (version is { } v)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{v}\"");
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    /// <summary>Sends a mutating call and returns the claim it answered with (asserting 200).</summary>
    public static async Task<ClaimDto> OkAsync(HttpClient client, HttpMethod method, string path, long version, object? body = null)
    {
        HttpResponseMessage response = await SendAsync(client, method, path, version, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ClaimDto claim = (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
        Assert.Equal($"\"{claim.Version}\"", response.Headers.ETag!.Tag); // the new ETag chains the next If-Match
        return claim;
    }

    public static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(status, response.StatusCode);
    }

    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = new(Db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    public Task<long> TimelineCountAsync(Guid claimId) =>
        ScalarAsync<long>("SELECT count(*) FROM claim_timeline WHERE claim_id = @id", ("id", claimId));
}

internal static class DateOnlyExtensions
{
    /// <summary>The wire format of a date (yyyy-MM-dd), independent of the machine's culture.</summary>
    public static string Iso(this DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
