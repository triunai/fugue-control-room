using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Diagnostics.Queries;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fugue.Tests.Api;

/// <summary>End to end through the REAL mediator (no fake sender): controller, logging and validation behaviors, handler.</summary>
public sealed class DiagnosticsPipelineTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Ping_returns_200_through_the_real_pipeline()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/diagnostics/ping?count=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new PingResponse("pong", 3), await response.Content.ReadFromJsonAsync<PingResponse>());
    }

    /// <summary>RED until the owner implements <c>ApiControllerBase.MapStatus</c> (expects 400 for Validation).</summary>
    [Fact]
    public async Task Ping_with_an_out_of_range_count_is_a_400_validation_problem()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/diagnostics/ping?count=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ["Count must be between 0 and 100."],
            json.RootElement.GetProperty("errors").GetProperty("count").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    /// <summary>Pinned catch: the framework's own 400 (model binding) must be problem+json, not application/json.</summary>
    [Fact]
    public async Task Framework_model_binding_400_is_problem_json_with_code_and_ids()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/diagnostics/ping?count=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation.failed", json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        Assert.True(json.RootElement.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Unknown_route_404_is_problem_json()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
