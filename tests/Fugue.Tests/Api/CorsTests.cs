using System.Net;
using Fugue.Tests.OpenApi;
using Mediator;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fugue.Tests.Api;

/// <summary>
/// The browser policy for the console (D-043): exact origins only, no credentials, the request headers the API reads, and the response
/// headers a client must be able to read. A preflight has no token, so it must be answered before authentication.
/// </summary>
public sealed class CorsTests
{
    private const string ConsoleOrigin = "http://localhost:4200";
    private const string ProdOrigin = "https://console.example.test";

    /// <summary>The real pipeline without a database. Only the throwing probe route (anonymous here, as in the other probe tests) needs the fallback policy opened.</summary>
    private static HttpClient Client(string? environment = null, params (string Key, string Value)[] settings) =>
        OpenApiSnapshot.Host(environment, settings).WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            if (settings.Any(s => s.Key == "Probe"))
            {
                TestAuth.OpenFallback(services);
            }

            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly); // for the throwing route
            services.RemoveAll<ISender>();
            services.AddSingleton<ISender, FakeSender>();
        })).CreateClient();

    private static HttpRequestMessage Preflight(string origin, string method, string headers, string path = "/api/v1/customers/01900000-0000-7000-8000-000000000001")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", method);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", headers);
        return request;
    }

    private static string[] Values(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? [.. values.SelectMany(v => v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))]
            : [];

    [Fact]
    public async Task The_console_dev_origin_gets_a_preflight_answer_for_a_conditional_update_without_a_token()
    {
        using HttpClient client = Client();

        HttpResponseMessage response = await client.SendAsync(Preflight(ConsoleOrigin, "PUT", "authorization,content-type,if-match,x-correlation-id,idempotency-key,x-saga-id"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); // answered by the CORS middleware, ahead of the 401 an unauthenticated request would get
        Assert.Equal([ConsoleOrigin], Values(response, "Access-Control-Allow-Origin"));
        Assert.Contains("PUT", Values(response, "Access-Control-Allow-Methods"));
        string[] allowed = [.. Values(response, "Access-Control-Allow-Headers").Select(h => h.ToLowerInvariant())];
        foreach (string header in new[] { "authorization", "content-type", "if-match", "x-correlation-id", "idempotency-key", "x-saga-id" })
        {
            Assert.Contains(header, allowed);
        }

        Assert.Empty(Values(response, "Access-Control-Allow-Credentials")); // bearer tokens in a header: no ambient credentials
        Assert.NotEmpty(Values(response, "Access-Control-Max-Age"));
    }

    [Fact]
    public async Task An_unlisted_origin_a_wildcard_style_origin_or_an_unlisted_header_gets_no_allow_headers()
    {
        using HttpClient client = Client();

        foreach (string origin in new[] { "http://localhost:4201", "https://evil.example.test", "http://localhost:4200.evil.test", "null" })
        {
            HttpResponseMessage response = await client.SendAsync(Preflight(origin, "GET", "authorization"));
            Assert.Empty(Values(response, "Access-Control-Allow-Origin"));
        }

        HttpResponseMessage badHeader = await client.SendAsync(Preflight(ConsoleOrigin, "GET", "x-evil"));
        Assert.DoesNotContain("x-evil", Values(badHeader, "Access-Control-Allow-Headers").Select(h => h.ToLowerInvariant()));
        HttpResponseMessage badMethod = await client.SendAsync(Preflight(ConsoleOrigin, "TRACE", "authorization"));
        Assert.DoesNotContain("TRACE", Values(badMethod, "Access-Control-Allow-Methods"));
    }

    [Fact]
    public async Task A_real_response_exposes_the_headers_a_client_needs_even_on_a_401()
    {
        using HttpClient client = Client();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers");
        request.Headers.TryAddWithoutValidation("Origin", ConsoleOrigin);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal([ConsoleOrigin], Values(response, "Access-Control-Allow-Origin"));
        string[] exposed = [.. Values(response, "Access-Control-Expose-Headers").Select(h => h.ToLowerInvariant())];
        foreach (string header in new[] { "etag", "link", "x-next-cursor", "x-correlation-id", "x-saga-id", "retry-after", "location" })
        {
            Assert.Contains(header, exposed);
        }

        Assert.Empty(Values(response, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Outside_Development_only_the_configured_origins_are_allowed_and_the_dev_origin_is_not()
    {
        using HttpClient client = Client("Production", ("Cors:Origins:0", ProdOrigin));

        HttpResponseMessage configured = await client.SendAsync(Preflight(ProdOrigin, "GET", "authorization"));
        HttpResponseMessage dev = await client.SendAsync(Preflight(ConsoleOrigin, "GET", "authorization"));

        Assert.Equal([ProdOrigin], Values(configured, "Access-Control-Allow-Origin"));
        Assert.Empty(Values(dev, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task With_nothing_configured_production_allows_no_cross_origin_caller()
    {
        using HttpClient client = Client("Production");

        HttpResponseMessage response = await client.SendAsync(Preflight(ProdOrigin, "GET", "authorization"));

        Assert.Empty(Values(response, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task A_configured_origin_is_added_to_the_dev_origin_in_Development()
    {
        using HttpClient client = Client(null, ("Cors:Origins:0", ProdOrigin));

        HttpResponseMessage configured = await client.SendAsync(Preflight(ProdOrigin, "GET", "authorization"));
        HttpResponseMessage dev = await client.SendAsync(Preflight(ConsoleOrigin, "GET", "authorization"));

        Assert.Equal([ProdOrigin], Values(configured, "Access-Control-Allow-Origin"));
        Assert.Equal([ConsoleOrigin], Values(dev, "Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://console.example.test/")]
    [InlineData("https://console.example.test/app")]
    [InlineData("console.example.test")]
    [InlineData("ftp://console.example.test")]
    [InlineData("https://user@console.example.test")]
    public void A_wildcard_or_malformed_origin_stops_the_host_from_starting(string origin)
    {
        Exception ex = Record.Exception(() => Client("Production", ("Cors:Origins:0", origin)).Dispose());

        Assert.NotNull(ex);
        Assert.Contains("Cors:Origins", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unhandled_exception_still_carries_the_cors_headers_so_the_console_can_read_the_problem()
    {
        using HttpClient client = Client("Production", ("Cors:Origins:0", ProdOrigin), ("Probe", "1"));
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/probe/boom");
        request.Headers.TryAddWithoutValidation("Origin", ProdOrigin);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal([ProdOrigin], Values(response, "Access-Control-Allow-Origin"));
        Assert.Contains("x-correlation-id", Values(response, "Access-Control-Expose-Headers").Select(h => h.ToLowerInvariant()));
    }
}
