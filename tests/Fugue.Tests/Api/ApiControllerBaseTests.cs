using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fugue.Tests.Api;

public sealed class ApiControllerBaseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiControllerBaseTests(WebApplicationFactory<Program> factory)
    {
        _client = ProbeFactory.CreateClient(factory);
    }

    [Fact]
    public async Task Send_returns_200_with_body_and_etag_for_versioned_value()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/probe/things/7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"3\"", response.Headers.ETag?.ToString());
        Thing? body = await response.Content.ReadFromJsonAsync<Thing>();
        Assert.Equal(new Thing(7, "widget", 3), body);
    }

    [Fact]
    public async Task SendCreated_returns_201_with_location_and_etag()
    {
        HttpResponseMessage response = await _client.PostAsync("/api/v1/probe/things?name=gizmo", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/probe/things/42", response.Headers.Location?.AbsolutePath, ignoreCase: true);
        Assert.Equal("\"1\"", response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task SendNoContent_returns_204()
    {
        HttpResponseMessage response = await _client.DeleteAsync("/api/v1/probe/things/7");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SendPaged_returns_items_with_cursor_header_and_link()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/probe/things?limit=2&cursor=old");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["a", "b"], await response.Content.ReadFromJsonAsync<string[]>());
        Assert.Equal("abc 1", response.Headers.GetValues("X-Next-Cursor").Single());

        string link = response.Headers.GetValues("Link").Single();
        Assert.EndsWith("/api/v1/probe/things?limit=2&cursor=abc%201>; rel=\"next\"", link, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("<http://localhost/", link, StringComparison.Ordinal);
        Assert.DoesNotContain("old", link, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"5\"", 5L)]
    [InlineData("W/\"5\"", 5L)]
    [InlineData("*", null)]
    [InlineData("\"abc\"", null)]
    public async Task IfMatch_is_parsed_and_reaches_the_handler(string header, long? expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/probe/echo");
        request.Headers.TryAddWithoutValidation("If-Match", header);

        Echo? echo = await (await _client.SendAsync(request)).Content.ReadFromJsonAsync<Echo>();

        Assert.Equal(expected, echo?.IfMatch);
    }

    [Fact]
    public async Task Missing_headers_reach_the_handler_as_null()
    {
        HttpResponseMessage response = await _client.PutAsync("/api/v1/probe/echo", content: null);

        Echo? echo = await response.Content.ReadFromJsonAsync<Echo>();
        Assert.Null(echo?.IfMatch);
        Assert.Null(echo?.IdempotencyKey);
    }

    [Fact]
    public async Task IdempotencyKey_reaches_the_handler()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/probe/echo");
        request.Headers.Add("Idempotency-Key", "key-123");

        Echo? echo = await (await _client.SendAsync(request)).Content.ReadFromJsonAsync<Echo>();

        Assert.Equal("key-123", echo?.IdempotencyKey);
    }

    [Fact]
    public async Task Failure_is_a_problem_json_with_code_type_and_trace_id()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/probe/fail?code=product.not_found");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = json.RootElement;
        Assert.Equal("product.not_found", root.GetProperty("code").GetString());
        Assert.Equal("https://fugue.dev/errors/product-not-found", root.GetProperty("type").GetString());
        Assert.Equal("Thing was not found.", root.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Validation_failure_carries_an_errors_dictionary()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/probe/invalid");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = json.RootElement;
        Assert.Equal("order.invalid", root.GetProperty("code").GetString());
        string[] messages = root.GetProperty("errors").GetProperty("quantity")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(["Must be positive."], messages);
    }
}
