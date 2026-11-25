using System.Net;
using System.Text.Json;
using Fugue.Api.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fugue.Tests.Api;

public sealed class GlobalExceptionHandlerTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Unhandled_exception_is_a_500_problem_without_exception_detail_in_production()
    {
        HttpClient client = ProbeFactory.CreateClient(factory, "Production");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/probe/boom");
        request.Headers.Add("X-Correlation-Id", "corr-boom");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("corr-boom", response.Headers.GetValues("X-Correlation-Id").Single());
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement root = json.RootElement;
        Assert.Equal("https://fugue.dev/errors/unexpected", root.GetProperty("type").GetString());
        Assert.Equal("unexpected", root.GetProperty("code").GetString());
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.Equal("corr-boom", root.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("secret detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
        Assert.False(root.TryGetProperty("exception", out _));
        Assert.False(root.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Client_abort_is_answered_with_499_and_no_body()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = cts.Token };
        var handler = new GlobalExceptionHandler(
            new ThrowingProblemDetailsService(), new TestEnvironment(), NullLogger<GlobalExceptionHandler>.Instance);

        bool handled = await handler.TryHandleAsync(context, new OperationCanceledException(cts.Token), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(499, context.Response.StatusCode);
    }

    private sealed class ThrowingProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context) => throw new InvalidOperationException("must not write");

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) => throw new InvalidOperationException("must not write");
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";

        public string ApplicationName { get; set; } = "Test";

        public string ContentRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
