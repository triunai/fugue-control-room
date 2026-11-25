using FluentResults;
using Fugue.Application.Common;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;

namespace Fugue.Application.Features.Webhooks;

/// <summary>Persistence contract for subscriptions. Reads are EF no-tracking projections; updates are tracked and guarded by the <c>xmin</c> token (ADR-006).</summary>
public interface IWebhookSubscriptionStore
{
    Task<WebhookSubscriptionDto> AddAsync(WebhookSubscription subscription, CancellationToken ct);

    Task<WebhookSubscriptionDto?> GetAsync(WebhookSubscriptionId id, CancellationToken ct);

    Task<IReadOnlyList<WebhookSubscriptionDto>> ListAsync(KeysetCursor? after, int take, CancellationToken ct);

    Task<WebhookSubscription?> FindForUpdateAsync(WebhookSubscriptionId id, CancellationToken ct);

    /// <summary>Saves a tracked change; when <paramref name="expectedVersion"/> is given and stale the result is <c>webhook.version_mismatch</c>.</summary>
    Task<Result<WebhookSubscriptionDto>> SaveAsync(WebhookSubscription subscription, long? expectedVersion, CancellationToken ct);

    Task<Result> DeleteAsync(WebhookSubscription subscription, long expectedVersion, CancellationToken ct);
}

/// <summary>Filters for the delivery list; every member is an exact match.</summary>
public sealed record WebhookDeliveryFilter(string? Status, WebhookSubscriptionId? SubscriptionId, string? EventType);

/// <summary>Delivery reads and the operator action on a dead letter.</summary>
public interface IWebhookDeliveryStore
{
    Task<IReadOnlyList<WebhookDeliveryDto>> ListAsync(WebhookDeliveryFilter filter, KeysetCursor? after, int take, CancellationToken ct);

    /// <summary>Revives a dead-lettered delivery with a fresh attempt budget and writes an audit row. NotFound or <c>webhook.delivery_not_dead</c> otherwise.</summary>
    Task<Result> RedeliverAsync(WebhookDeliveryId id, CancellationToken ct);
}

/// <summary>Encrypts and decrypts subscription secrets (Data Protection in Infrastructure). A ciphertext that cannot be read throws.</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}

/// <summary>The SSRF guard (ADR-011): https only, and no private, loopback, link-local or otherwise non-public destination. Implemented in Infrastructure (DNS).</summary>
public interface IWebhookUrlGuard
{
    /// <summary>Success, or a validation failure (<c>webhook.url_rejected</c>) that never echoes resolved addresses.</summary>
    Task<Result> CheckAsync(string url, CancellationToken ct);
}
