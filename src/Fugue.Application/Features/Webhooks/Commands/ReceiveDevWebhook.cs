using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Webhooks.Helpers;
using Fugue.Application.Features.Webhooks.Repos;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Webhooks.Commands;

/// <summary>
/// Contract. The receiving side of the signed-webhook scheme as the documentation tells a receiver to implement it: verify the signature in constant time
/// over the raw body, reject a timestamp outside the 5 minute window, dedupe on the event id. A plain request, not a command: it is a test double, so
/// it is not audited (a bad-signature flood must not write audit rows). The endpoint answers 404 outside Development.
/// </summary>
public sealed record ReceiveDevWebhookRequest(
    string? EventId, string? EventType, string? Timestamp, string? Signature, string? CorrelationId, string? SagaId, string? Traceparent, byte[] Body)
    : IRequest<Result<DevWebhookReceiptDto>>;

public sealed class ReceiveDevWebhookValidator : AbstractValidator<ReceiveDevWebhookRequest>
{
    public const int MaxBodyBytes = 256 * 1024;

    public ReceiveDevWebhookValidator()
    {
        RuleFor(x => x.Body).NotNull().Must(b => b.Length <= MaxBodyBytes).WithMessage($"The body may be at most {MaxBodyBytes} bytes.");
        RuleFor(x => x.EventId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(100);
    }
}

public sealed class ReceiveDevWebhookHandler(IDevWebhookSink sink, TimeProvider time) : IRequestHandler<ReceiveDevWebhookRequest, Result<DevWebhookReceiptDto>>
{
    public ValueTask<Result<DevWebhookReceiptDto>> Handle(ReceiveDevWebhookRequest request, CancellationToken cancellationToken)
    {
        if (sink.Secret is not { } secret)
        {
            return Fail(FugueErrors.Unavailable("sink.no_secret", "Tell the sink which secret to trust first: PUT /api/v1/dev/webhook-sink/secret."));
        }

        if (!long.TryParse(request.Timestamp, out long timestamp))
        {
            return Fail(FugueErrors.Unauthorized("sink.malformed", "X-Fugue-Timestamp must be unix seconds."));
        }

        // Signature first: nothing about freshness is revealed to a caller who cannot sign.
        if (!WebhookSecretFormat.Verify(secret, timestamp, request.Body, request.Signature))
        {
            return Fail(FugueErrors.Unauthorized("sink.bad_signature", "The signature does not match."));
        }

        // Only the body is signed. A replay that changes just the X-Fugue-Event-* headers would dodge the dedupe, so the headers must repeat the signed envelope.
        if (!MatchesSignedEnvelope(request))
        {
            return Fail(FugueErrors.Unauthorized("sink.envelope_mismatch", "The event id and type headers must match the signed body."));
        }

        DateTimeOffset now = time.GetUtcNow();
        if ((now - DateTimeOffset.FromUnixTimeSeconds(timestamp)).Duration() > WebhookSecretFormat.ReplayWindow)
        {
            return Fail(FugueErrors.Unauthorized("sink.stale_timestamp", "The timestamp is outside the 5 minute replay window."));
        }

        bool first = sink.TryAdd(new ReceivedWebhookDto(
            request.EventId!, request.EventType!, now, timestamp, request.CorrelationId, request.SagaId, request.Traceparent, System.Text.Encoding.UTF8.GetString(request.Body)));
        return ValueTask.FromResult(Result.Ok(new DevWebhookReceiptDto(first ? "accepted" : "duplicate", request.EventId!)));
    }

    private static bool MatchesSignedEnvelope(ReceiveDevWebhookRequest request)
    {
        try
        {
            using var envelope = System.Text.Json.JsonDocument.Parse(request.Body);
            return envelope.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && envelope.RootElement.TryGetProperty("id", out System.Text.Json.JsonElement id) && id.GetString() == request.EventId
                && envelope.RootElement.TryGetProperty("type", out System.Text.Json.JsonElement type) && type.GetString() == request.EventType;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static ValueTask<Result<DevWebhookReceiptDto>> Fail(FugueError error) => ValueTask.FromResult(Result.Fail<DevWebhookReceiptDto>(error));
}
