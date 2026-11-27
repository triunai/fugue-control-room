using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Common;
using Fugue.Application.Features.Webhooks.Models;
using Fugue.Application.Features.Webhooks.Repos;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>
/// Delivery reads (EF, no tracking, keyset on <c>(occurred_at, id)</c> newest first) and the operator action on a dead letter. The redeliver is a Dapper
/// <c>UPDATE ... WHERE status = 'dead'</c> (one statement, so two operators cannot both revive it) plus an explicit <c>audit_log</c> row in the same
/// transaction (the entity is <c>[NoAudit]</c>, so the interceptor does not see it).
/// </summary>
internal sealed class WebhookDeliveryStore(FugueDbContext db, SqlExecutor sql, IAuditTrail audit, TimeProvider time) : IWebhookDeliveryStore
{
    private static readonly string ReviveSql = SqlScripts.Get("webhooks/revive-delivery");

    public async Task<IReadOnlyList<WebhookDeliveryDto>> ListAsync(WebhookDeliveryFilter filter, KeysetCursor? after, int take, CancellationToken ct)
    {
        IQueryable<WebhookDelivery> deliveries = after is { } cursor
            // Row-value comparison is not expressible in LINQ; the filters compose on top of this.
            ? db.WebhookDeliveries.FromSqlRaw(SqlScripts.Get("webhooks/list-deliveries-before"), new NpgsqlParameter("createdAt", cursor.CreatedAt), new NpgsqlParameter("id", cursor.Id))
            : db.WebhookDeliveries;

        deliveries = deliveries.AsNoTracking();
        if (filter.Status is { } status)
        {
            deliveries = deliveries.Where(d => d.Status == status);
        }

        if (filter.SubscriptionId is { } subscription)
        {
            deliveries = deliveries.Where(d => d.SubscriptionId == subscription);
        }

        var query = deliveries.Join(db.OutboxMessages.AsNoTracking(), d => d.MessageId, m => m.Id, (d, m) => new { Delivery = d, m.EventType, m.AggregateId });
        if (filter.EventType is { } eventType)
        {
            query = query.Where(x => x.EventType == eventType);
        }

        var rows = await query.OrderByDescending(x => x.Delivery.OccurredAt).ThenByDescending(x => x.Delivery.Id).Take(take).ToListAsync(ct);
        return rows.Select(x => new WebhookDeliveryDto(
            x.Delivery.Id, x.Delivery.MessageId.Value, x.EventType, x.AggregateId, x.Delivery.SubscriptionId, x.Delivery.OccurredAt, x.Delivery.Status,
            x.Delivery.Attempts, x.Delivery.NextAttemptAt, x.Delivery.LastStatusCode, x.Delivery.LastError, x.Delivery.DurationMs, x.Delivery.DeliveredAt,
            x.Delivery.DeadLetteredAt)).ToList();
    }

    public async Task<Result> RedeliverAsync(WebhookDeliveryId id, CancellationToken ct)
    {
        IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            int revived = await sql.ExecuteAsync(ReviveSql, new { Id = id, Now = time.GetUtcNow() }, ct);
            if (revived == 0)
            {
                bool exists = await db.WebhookDeliveries.AsNoTracking().AnyAsync(d => d.Id == id, ct);
                return Result.Fail(exists ? WebhookErrors.DeliveryNotDead() : WebhookErrors.DeliveryNotFound(id));
            }

            await audit.RecordAsync(
                new AuditEntry(
                    nameof(WebhookDelivery),
                    id.ToString(),
                    new Dictionary<string, AuditFieldChange>
                    {
                        [nameof(WebhookDelivery.Status)] = AuditFieldChange.Changed(WebhookDeliveryStatuses.Dead, WebhookDeliveryStatuses.Pending),
                    },
                    typeof(WebhookDelivery),
                    "webhooks.redeliver"),
                ct);
            if (own is not null)
            {
                await own.CommitAsync(ct);
            }

            return Result.Ok();
        }
        finally
        {
            if (own is not null)
            {
                await own.DisposeAsync(); // not committed: rolls back
            }
        }
    }
}
