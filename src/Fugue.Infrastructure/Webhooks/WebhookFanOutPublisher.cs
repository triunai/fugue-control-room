using Fugue.Application.Abstractions;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>
/// The webhook adapter of <see cref="IEventPublisher"/> (ADR-011): "publishing" an outbox message to webhooks means recording one pending
/// <c>webhook_deliveries</c> row per ACTIVE subscription whose filters match. It runs inside the relay's transaction, so the deliveries and the
/// message's <c>dispatched</c> status commit together, and <c>ON CONFLICT DO NOTHING</c> on <c>(message_id, subscription_id)</c> makes a repeat harmless.
/// The active subscriptions are read once per relay run (the publisher is scoped to it).
/// </summary>
internal sealed class WebhookFanOutPublisher(FugueDbContext db, SqlExecutor sql, TimeProvider time) : IEventPublisher
{
    private const string InsertSql = """
        INSERT INTO webhook_deliveries (id, message_id, subscription_id, occurred_at, status, attempts, next_attempt_at)
        VALUES (@Id, @MessageId, @SubscriptionId, @Now, 'pending', 0, @Now)
        ON CONFLICT (message_id, subscription_id) DO NOTHING
        """;

    private IReadOnlyList<(WebhookSubscriptionId Id, string[] EventTypes)>? _subscriptions;

    public async Task PublishAsync(OutboxEvent message, CancellationToken ct)
    {
        _subscriptions ??= (await db.WebhookSubscriptions.AsNoTracking().Where(s => s.IsActive).Select(s => new { s.Id, s.EventTypes }).ToListAsync(ct))
            .Select(s => (s.Id, s.EventTypes)).ToList();

        DateTimeOffset now = time.GetUtcNow();
        foreach ((WebhookSubscriptionId id, string[] eventTypes) in _subscriptions)
        {
            if (EventTypeFilter.MatchesAny(eventTypes, message.EventType))
            {
                await sql.ExecuteAsync(InsertSql, new { Id = WebhookDeliveryId.New(), MessageId = message.Id, SubscriptionId = id, Now = now }, ct);
            }
        }
    }
}
