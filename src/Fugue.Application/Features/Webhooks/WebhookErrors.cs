using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Webhooks;

/// <summary>The stable error codes of the Webhooks slice.</summary>
public static class WebhookErrors
{
    public static FugueError SubscriptionNotFound(WebhookSubscriptionId id) =>
        FugueErrors.NotFound("webhook.subscription_not_found", $"Webhook subscription {id} was not found.");

    public static FugueError DeliveryNotFound(WebhookDeliveryId id) =>
        FugueErrors.NotFound("webhook.delivery_not_found", $"Webhook delivery {id} was not found.");

    public static FugueError DeliveryNotDead() =>
        FugueErrors.Conflict("webhook.delivery_not_dead", "Only a dead-lettered delivery can be redelivered.");

    public static FugueError VersionMismatch() =>
        FugueErrors.PreconditionFailed(
            "webhook.version_mismatch", "The subscription changed since it was read. Re-read it and retry with the new ETag.");

    public static FugueError IfMatchRequired() =>
        FugueErrors.PreconditionRequired("webhook.if_match_required", "This operation requires an If-Match header with the current ETag.");

    /// <summary>The SSRF guard refused the URL. The reason is generic on purpose: it never echoes resolved addresses.</summary>
    public static FugueError UrlRejected(string reason) =>
        FugueErrors.Validation("webhook.url_rejected", "The webhook URL is not allowed.", new Dictionary<string, string[]> { ["url"] = [reason] });
}
