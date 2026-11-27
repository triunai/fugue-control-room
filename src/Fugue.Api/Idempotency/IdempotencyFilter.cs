using System.Security.Cryptography;
using System.Text.Json;
using Fugue.Api.Http;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Infrastructure.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace Fugue.Api.Idempotency;

/// <summary>
/// The <c>[Idempotent]</c> resource filter (ADR-013). A resource filter runs after authorization and BEFORE model binding (so the body can be buffered,
/// fingerprinted and rewound) and its <c>next()</c> returns AFTER the result executed (so status, <c>Location</c>, <c>ETag</c>, <c>Content-Type</c> and
/// the serialized bytes exist). It claims the key first, runs the action against a buffered response body, then completes the claim (2xx), frees it
/// (a 4xx the API itself produced: nothing was committed) or leaves it pending (anything ambiguous: 5xx, exception, abort, oversize, storage failure).
/// A pending key is never re-run. Without the header the action runs untouched.
/// </summary>
public sealed partial class IdempotencyFilter(IIdempotencyStore store, ICurrentUser user, IOptions<IdempotencyOptions> options, ILogger<IdempotencyFilter> logger)
    : IAsyncResourceFilter
{
    public const string ReplayedHeader = "Idempotent-Replayed";

    private const string ErrorTypeBase = "https://fugue.dev/errors/";
    private const int MaxHeaderValueLength = 2048;

    private static readonly string[] StoredHeaders = ["Location", "ETag", "Content-Type"];

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        HttpContext http = context.HttpContext;
        HeaderParse header = IdempotencyKeyHeader.Parse(http.Request.Headers[IdempotencyKeyHeader.Name]);
        if (!header.IsPresent)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().Any(a => a.Required))
            {
                context.Result = Problem(
                    http, StatusCodes.Status400BadRequest, "idempotency.key_required",
                    "This operation requires an Idempotency-Key header, so a retry cannot create a second resource.");
                return;
            }

            await next();
            return;
        }

        if (!header.IsValid)
        {
            context.Result = Problem(
                http, StatusCodes.Status400BadRequest, "idempotency.key_invalid",
                $"The Idempotency-Key header must be one quoted string or a plain token of 1 to {IdempotencyKeyHeader.MaxLength} printable characters (letters, digits, '.', '_', ':', '-').");
            return;
        }

        IdempotencyOptions settings = options.Value;
        if (http.Request.ContentLength > settings.MaxRequestBytes || await ReadBodyAsync(http, settings.MaxRequestBytes) is not { } body)
        {
            context.Result = Problem(
                http, StatusCodes.Status413PayloadTooLarge, "idempotency.body_too_large",
                $"A request with an Idempotency-Key may carry at most {settings.MaxRequestBytes} bytes.");
            return;
        }

        Guid actor = user.UserId ?? throw new InvalidOperationException("An idempotent action ran without an authenticated user.");
        string endpoint = $"{http.Request.Method} {context.ActionDescriptor.AttributeRouteInfo?.Template?.ToLowerInvariant()}"; // the template as routed ("api/v1/Customers"), lower-cased so the scope is stable
        IdempotencyClaim claim = await store.ClaimAsync(actor, endpoint, header.Key!, SHA256.HashData(body), http.RequestAborted);

        switch (claim.Decision)
        {
            case IdempotencyDecision.Execute:
                await ExecuteAsync(context, next, claim);
                break;
            case IdempotencyDecision.Replay:
                context.Result = Replay(http, claim);
                break;
            case IdempotencyDecision.KeyReused:
                context.Result = Problem(http, StatusCodes.Status422UnprocessableEntity, "idempotency.key_reused", "This Idempotency-Key was already used with a different request body.");
                break;
            case IdempotencyDecision.InProgress:
                http.Response.Headers.RetryAfter = "1";
                context.Result = Problem(http, StatusCodes.Status409Conflict, "idempotency.in_progress", "A request with this Idempotency-Key is still being processed. Retry shortly.");
                break;
            case IdempotencyDecision.OutcomeUnknown:
                context.Result = Problem(
                    http, StatusCodes.Status409Conflict, "idempotency.outcome_unknown",
                    "An earlier request with this Idempotency-Key did not finish and may or may not have taken effect. Check whether the resource exists; if it does not, retry with a new Idempotency-Key.");
                break;
            default:
                context.Result = ReplayUnavailable(http);
                break;
        }
    }

    private async Task ExecuteAsync(ResourceExecutingContext context, ResourceExecutionDelegate next, IdempotencyClaim claim)
    {
        HttpContext http = context.HttpContext;
        HttpResponse response = http.Response;
        Stream original = response.Body;
        using var buffer = new MemoryStream();
        response.Body = buffer;
        ResourceExecutedContext executed;
        try
        {
            executed = await next();
            await response.BodyWriter.FlushAsync(CancellationToken.None);
        }
        finally
        {
            response.Body = original;
        }

        if (executed.Exception is not null || http.RequestAborted.IsCancellationRequested)
        {
            return; // outcome unknown: the key stays pending, the exception goes on to the exception handler
        }

        int status = response.StatusCode;
        if (status is >= 200 and < 300)
        {
            response.Headers.CacheControl = "no-store";
            await TryCompleteAsync(http, claim, status, buffer);
        }
        else if (executed.Result is ProblemResult && status is >= 400 and < 500)
        {
            await TryReleaseAsync(http, claim);
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(original, http.RequestAborted);
    }

    private async Task TryCompleteAsync(HttpContext http, IdempotencyClaim claim, int status, MemoryStream buffer)
    {
        try
        {
            if (buffer.Length > options.Value.MaxResponseBytes || SafeHeaders(http.Response) is not { } headers)
            {
                LogNotStored(logger, claim.RecordId);
                return;
            }

            if (!await store.CompleteAsync(claim.RecordId, claim.ClaimToken, status, JsonSerializer.Serialize(headers), buffer.ToArray(), http.RequestAborted))
            {
                LogClaimLost(logger, claim.RecordId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogStoreFailed(logger, ex, claim.RecordId);
        }
    }

    private async Task TryReleaseAsync(HttpContext http, IdempotencyClaim claim)
    {
        try
        {
            await store.ReleaseAsync(claim.RecordId, claim.ClaimToken, http.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogStoreFailed(logger, ex, claim.RecordId);
        }
    }

    /// <summary>The allowlisted headers of the response, or null when one holds a control character (it would be replayed to a client verbatim).</summary>
    private static Dictionary<string, string>? SafeHeaders(HttpResponse response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in StoredHeaders)
        {
            string value = name == "Content-Type" ? response.ContentType ?? string.Empty : response.Headers[name].ToString();
            if (value.Length == 0)
            {
                continue;
            }

            if (!IsSafeHeaderValue(value))
            {
                return null;
            }

            headers[name] = value;
        }

        return headers;
    }

    private static bool IsSafeHeaderValue(string value) =>
        value.Length <= MaxHeaderValueLength && value.All(c => c is >= ' ' and not '\u007f');

    private IActionResult Replay(HttpContext http, IdempotencyClaim claim)
    {
        Dictionary<string, string>? stored;
        try
        {
            stored = claim.ResponseHeaders is null ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(claim.ResponseHeaders);
        }
        catch (JsonException)
        {
            stored = null;
        }

        if (stored is null || claim.ResponseBody is null || stored.Any(h => !StoredHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase) || !IsSafeHeaderValue(h.Value)))
        {
            return ReplayUnavailable(http);
        }

        return new ReplayResult(claim.ResponseStatus, stored, claim.ResponseBody);
    }

    private ProblemResult ReplayUnavailable(HttpContext http) =>
        Problem(http, StatusCodes.Status500InternalServerError, "idempotency.replay_unavailable", "The stored response for this Idempotency-Key cannot be read. The key is kept; retry with a new Idempotency-Key only after checking whether the resource exists.");

    /// <summary>Buffers the request body (up to <paramref name="maxBytes"/>) and rewinds it for model binding; null when it is larger.</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpContext http, int maxBytes)
    {
        http.Request.EnableBuffering();
        byte[] body = await http.Request.ReadBodyAsync(maxBytes, http.RequestAborted);
        if (body.Length > maxBytes)
        {
            return null;
        }

        http.Request.Body.Position = 0;
        return body;
    }

    private ProblemResult Problem(HttpContext http, int status, string code, string detail)
    {
        var factory = http.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        ProblemDetails problem = factory.CreateProblemDetails(http, status, detail, ErrorTypeBase + code.Replace('.', '-').Replace('_', '-'));
        problem.Extensions["code"] = code;
        ProblemDetailsEnrichment.AddCorrelation(problem, http);
        if (user.TenantId is { } tenantId)
        {
            problem.Extensions["tenantId"] = tenantId;
        }

        return new ProblemResult(problem);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Idempotency record {RecordId}: the response was not stored (too large or an unsafe header); the key stays pending")]
    private static partial void LogNotStored(ILogger logger, Guid recordId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Idempotency record {RecordId}: the claim was lost before completion; the stored state was left alone")]
    private static partial void LogClaimLost(ILogger logger, Guid recordId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Idempotency record {RecordId}: the store failed after the action ran; the key stays pending")]
    private static partial void LogStoreFailed(ILogger logger, Exception exception, Guid recordId);
}

/// <summary>Writes a stored response: status, the allowlisted headers, <c>Idempotent-Replayed: true</c>, <c>Cache-Control: no-store</c> and the exact bytes.</summary>
internal sealed class ReplayResult(int status, IReadOnlyDictionary<string, string> headers, byte[] body) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        HttpResponse response = context.HttpContext.Response;
        response.StatusCode = status;
        foreach ((string name, string value) in headers)
        {
            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                response.ContentType = value;
            }
            else
            {
                response.Headers[name] = value;
            }
        }

        response.Headers[IdempotencyFilter.ReplayedHeader] = "true";
        response.Headers.CacheControl = "no-store";
        response.ContentLength = body.Length;
        await response.Body.WriteAsync(body, context.HttpContext.RequestAborted);
    }
}
