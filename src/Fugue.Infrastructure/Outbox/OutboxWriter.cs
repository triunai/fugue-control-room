using Fugue.Application.Abstractions;
using Fugue.Domain.Events;
using Fugue.Domain.Webhooks;
using Fugue.Infrastructure.Persistence;

namespace Fugue.Infrastructure.Outbox;

/// <summary>
/// The Dapper write path of <c>outbox_messages</c> (ADR-006 insert path, ADR-011): runs through <see cref="SqlExecutor"/>, so inside a unit of
/// work (or the repository's short transaction) the row commits or rolls back with the change. Baselined in <c>raw-sql-writes</c>: outbox rows
/// are operational plumbing (<c>[NoAudit]</c>), not business data.
/// </summary>
internal sealed class OutboxWriter(SqlExecutor sql, OutboxRowFactory factory) : IOutbox
{
    private const string InsertSql = """
        INSERT INTO outbox_messages (id, occurred_at, event_type, aggregate_type, aggregate_id, payload, correlation_id, saga_id, traceparent,
                                     status, attempts, next_attempt_at)
        VALUES (@Id, @OccurredAt, @EventType, @AggregateType, @AggregateId, CAST(@Payload AS jsonb), @CorrelationId, @SagaId, @Traceparent,
                @Status, 0, @NextAttemptAt)
        """;

    public async Task EnqueueAsync(IHasDomainEvents aggregate, CancellationToken ct)
    {
        foreach (IDomainEvent evt in aggregate.DomainEvents)
        {
            OutboxMessage row = factory.Create(evt);
            await sql.ExecuteAsync(
                InsertSql,
                new
                {
                    Id = row.Id,
                    row.OccurredAt,
                    row.EventType,
                    row.AggregateType,
                    row.AggregateId,
                    row.Payload,
                    row.CorrelationId,
                    row.SagaId,
                    row.Traceparent,
                    row.Status,
                    row.NextAttemptAt,
                },
                ct);
        }

        aggregate.ClearDomainEvents();
    }
}
