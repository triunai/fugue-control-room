using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fugue.Tests.Api;

public sealed class CorrelationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = ProbeFactory.CreateClient(factory);

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.Single() : string.Empty;

    [Fact]
    public async Task Correlation_id_is_generated_when_absent()
    {
        HttpResponseMessage response = await _client.PutAsync("/api/v1/probe/echo", content: null);

        string id = Header(response, "X-Correlation-Id");
        Assert.Matches("^[A-Za-z0-9_-]{1,64}$", id);
    }

    [Fact]
    public async Task Valid_inbound_correlation_id_is_echoed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/probe/echo");
        request.Headers.Add("X-Correlation-Id", "req-ABC_123");

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal("req-ABC_123", Header(response, "X-Correlation-Id"));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("semi;colon")]
    public async Task Malicious_inbound_correlation_id_is_replaced(string inbound)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/probe/echo");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", inbound);

        HttpResponseMessage response = await _client.SendAsync(request);

        string id = Header(response, "X-Correlation-Id");
        Assert.NotEqual(inbound, id);
        Assert.Matches("^[A-Za-z0-9_-]{1,64}$", id);
    }

    [Fact]
    public async Task Overlong_inbound_correlation_id_is_replaced()
    {
        string inbound = new('a', 65);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/probe/echo");
        request.Headers.Add("X-Correlation-Id", inbound);

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.NotEqual(inbound, Header(response, "X-Correlation-Id"));
    }

    [Fact]
    public async Task Valid_inbound_saga_id_is_echoed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/probe/echo");
        request.Headers.Add("X-Saga-Id", "order-42");

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal("order-42", Header(response, "X-Saga-Id"));
    }

    [Fact]
    public async Task Saga_id_is_absent_when_not_supplied_and_malicious_values_are_ignored()
    {
        HttpResponseMessage none = await _client.PutAsync("/api/v1/probe/echo", content: null);
        Assert.False(none.Headers.Contains("X-Saga-Id"));

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/probe/echo");
        request.Headers.TryAddWithoutValidation("X-Saga-Id", "bad value!");
        HttpResponseMessage bad = await _client.SendAsync(request);
        Assert.False(bad.Headers.Contains("X-Saga-Id"));
    }

    [Fact]
    public async Task Saga_id_set_by_a_handler_is_echoed()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/probe/saga");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("order-123", Header(response, "X-Saga-Id"));
    }

    [Fact]
    public async Task Framework_generated_problems_carry_trace_and_correlation_ids()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/probe/things?limit=abc");
        request.Headers.Add("X-Correlation-Id", "corr-1");

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("corr-1", json.RootElement.GetProperty("correlationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }
}
