using System.Diagnostics;
using System.Net.Http.Headers;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Webhooks.Helpers;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>The outcome of one send. <see cref="Error"/> is clipped and never holds a header, the body or the secret.</summary>
internal sealed record SendResult(bool Success, int? StatusCode, string? Error, int DurationMs);

/// <summary>
/// Signs and POSTs one event (ADR-011). A typed <see cref="HttpClient"/>: the primary handler dials only verified addresses and never follows a
/// redirect (<see cref="GuardedConnect"/>), and the standard resilience handler adds timeouts and a per-authority circuit breaker. Headers:
/// <c>X-Fugue-Event-Id</c>, <c>-Event-Type</c>, <c>-Timestamp</c> (unix seconds), <c>-Signature</c> (<c>v1=hex HMAC-SHA256(secret, timestamp.rawBody)</c>),
/// <c>X-Correlation-Id</c>, <c>X-Saga-Id</c>, <c>traceparent</c>. A 2xx is success; everything else (including a 3xx, which is not followed) is a failed attempt.
/// </summary>
internal sealed class WebhookSender(HttpClient http, TimeProvider time)
{
    private const int MaxErrorChars = 500;

    public async Task<SendResult> SendAsync(Uri url, string secret, OutboxEvent message, CancellationToken ct)
    {
        byte[] body = WebhookEnvelope.Build(message);
        long timestamp = time.GetUtcNow().ToUnixTimeSeconds();

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.UserAgent.ParseAdd("Fugue-Webhooks/1");
        request.Headers.TryAddWithoutValidation("X-Fugue-Event-Id", message.Id.ToString());
        request.Headers.TryAddWithoutValidation("X-Fugue-Event-Type", message.EventType);
        request.Headers.TryAddWithoutValidation("X-Fugue-Timestamp", timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("X-Fugue-Signature", WebhookSecretFormat.Sign(secret, timestamp, body));
        request.Headers.TryAddWithoutValidation("X-Saga-Id", message.SagaId);
        if (message.CorrelationId is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", message.CorrelationId);
        }

        if (message.Traceparent is not null)
        {
            request.Headers.TryAddWithoutValidation("traceparent", message.Traceparent);
        }

        long started = Stopwatch.GetTimestamp();
        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            int code = (int)response.StatusCode;
            string? error = response.IsSuccessStatusCode ? null : code is >= 300 and < 400 ? $"HTTP {code}: redirects are not followed" : $"HTTP {code}";
            return new SendResult(error is null, code, error, Elapsed(started));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // shutting down: the lease expires and the attempt is retried
        }
        catch (Exception ex)
        {
            // Timeouts (Polly TimeoutRejectedException), an open circuit (BrokenCircuitException), a refused connection, a blocked destination.
            string text = $"{ex.GetType().Name}: {ex.Message}";
            return new SendResult(false, null, text.Length <= MaxErrorChars ? text : text[..MaxErrorChars], Elapsed(started));
        }
    }

    private static int Elapsed(long started) => (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
