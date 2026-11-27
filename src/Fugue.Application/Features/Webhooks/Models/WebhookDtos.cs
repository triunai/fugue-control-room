using Fugue.Application.Features.Webhooks.Helpers;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Webhooks.Models;

/// <summary>A subscription as returned to clients. It never carries the secret; <see cref="Version"/> is also sent as the <c>ETag</c>.</summary>
public sealed record WebhookSubscriptionDto(
    WebhookSubscriptionId Id,
    string Name,
    string Url,
    IReadOnlyList<string> EventTypes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version) : IVersioned;

/// <summary>
/// The ONLY place the secret leaves the system: the response to create and to rotate. It is not stored in the clear and cannot be read again,
/// so the caller must keep it. Use <see cref="WebhookSecretFormat"/> to sign and verify.
/// </summary>
public sealed record WebhookSubscriptionSecretDto(
    WebhookSubscriptionId Id,
    string Name,
    string Url,
    IReadOnlyList<string> EventTypes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version,
    string Secret) : IVersioned
{
    public static WebhookSubscriptionSecretDto From(WebhookSubscriptionDto s, string secret) =>
        new(s.Id, s.Name, s.Url, s.EventTypes, s.IsActive, s.CreatedAt, s.CreatedBy, s.UpdatedAt, s.UpdatedBy, s.Version, secret);
}

/// <summary>One delivery attempt state. No headers, no body, no secret: the clipped error is the only free text.</summary>
public sealed record WebhookDeliveryDto(
    WebhookDeliveryId Id,
    Guid MessageId,
    string EventType,
    string AggregateId,
    WebhookSubscriptionId SubscriptionId,
    DateTimeOffset OccurredAt,
    string Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    int? LastStatusCode,
    string? LastError,
    int? DurationMs,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? DeadLetteredAt);
