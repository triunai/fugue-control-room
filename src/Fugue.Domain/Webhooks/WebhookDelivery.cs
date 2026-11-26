using Fugue.Domain.Audit;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Webhooks;

/// <summary>
/// One (message, subscription) pair and its delivery state (ADR-011). Created by the relay when it fans a message out, moved by the
/// delivery worker (lease, attempts, backoff, dead letter) and by the redeliver endpoint. Not <c>IAuditable</c>: it has
/// <see cref="OccurredAt"/> (fan-out time); operator actions on it are written to <c>audit_log</c> explicitly.
/// </summary>
[NoAudit]
public sealed class WebhookDelivery
{
    public WebhookDeliveryId Id { get; set; }

    public OutboxMessageId MessageId { get; set; }

    public WebhookSubscriptionId SubscriptionId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string Status { get; set; } = WebhookDeliveryStatuses.Pending;

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>While <c>in_flight</c>: when the claim expires and another worker may take the delivery (crash recovery).</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// A fresh random value written at every claim and required by the result write, so a worker whose claim was superseded (lease expiry, then a
    /// redelivery that restarts <see cref="Attempts"/>) can never overwrite the outcome of a later attempt. Cleared when the attempt ends.
    /// </summary>
    public Guid? ClaimToken { get; set; }

    public int? LastStatusCode { get; set; }

    /// <summary>Clipped; never holds headers, the secret or the body.</summary>
    public string? LastError { get; set; }

    public int? DurationMs { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public DateTimeOffset? DeadLetteredAt { get; set; }
}

public static class WebhookDeliveryStatuses
{
    public const string Pending = "pending";
    public const string InFlight = "in_flight";
    public const string Succeeded = "succeeded";
    public const string Dead = "dead";
}
