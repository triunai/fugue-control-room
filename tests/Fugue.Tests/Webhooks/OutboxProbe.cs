using System.Text.Json;
using Fugue.Tests.Persistence;
using Npgsql;

namespace Fugue.Tests.Webhooks;

/// <summary>One <c>outbox_messages</c> row as the tests read it straight from Postgres.</summary>
public sealed record OutboxRow(
    Guid Id, string EventType, string AggregateType, string AggregateId, JsonElement Payload, string? CorrelationId, string SagaId, string? Traceparent,
    string Status, int Attempts, DateTimeOffset NextAttemptAt, string? LastError, DateTimeOffset? DispatchedAt);

internal static class OutboxProbe
{
    public static async Task<List<OutboxRow>> ByAggregateAsync(PostgresFixture db, string aggregateId)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT id, event_type, aggregate_type, aggregate_id, payload::text, correlation_id, saga_id, traceparent, status, attempts, next_attempt_at, last_error, dispatched_at " +
            "FROM outbox_messages WHERE aggregate_id = @a ORDER BY occurred_at, id",
            connection);
        command.Parameters.AddWithValue("a", aggregateId);
        var rows = new List<OutboxRow>();
        await using NpgsqlDataReader r = await command.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new OutboxRow(
                r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), JsonDocument.Parse(r.GetString(4)).RootElement.Clone(),
                r.IsDBNull(5) ? null : r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetString(8), r.GetInt32(9),
                r.GetFieldValue<DateTimeOffset>(10), r.IsDBNull(11) ? null : r.GetString(11), r.IsDBNull(12) ? null : r.GetFieldValue<DateTimeOffset>(12)));
        }

        return rows;
    }
}
