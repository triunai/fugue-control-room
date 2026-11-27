using Fugue.Application.Features.Audit.Repos;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Audit;

/// <summary>
/// "The Score" (D-064) read through EF only (ADR-006): one raw statement, a UNION over <c>audit_log</c>, <c>outbox_messages</c> and the
/// <c>webhook_deliveries</c> of those messages, ordered by time then by voice (command, event, delivery), capped at 500 rows.
/// </summary>
internal sealed class AuditTraceReader(FugueDbContext db) : IAuditTraceReader
{
    public async Task<IReadOnlyList<AuditTraceEntry>> ReadAsync(string correlationId, CancellationToken ct)
    {
        List<Row> rows = await db.Database.SqlQueryRaw<Row>(SqlScripts.Get("audit/trace"), new NpgsqlParameter("correlationId", correlationId)).ToListAsync(ct);
        return [.. rows.Select(r => new AuditTraceEntry(
            r.Kind, r.Id, r.At, r.EndAt, r.Label, r.Detail, r.Status, r.DurationMs, r.Attempts, r.StatusCode, r.ErrorCode, r.EntityType, r.EntityId))];
    }

    /// <summary>The raw-SQL row; properties follow the snake_case column aliases through the model's naming convention.</summary>
    private sealed class Row
    {
        public int Rank { get; set; }

        public string Kind { get; set; } = string.Empty;

        public Guid Id { get; set; }

        public DateTimeOffset At { get; set; }

        public DateTimeOffset? EndAt { get; set; }

        public string Label { get; set; } = string.Empty;

        public string? Detail { get; set; }

        public string Status { get; set; } = string.Empty;

        public int? DurationMs { get; set; }

        public int? Attempts { get; set; }

        public int? StatusCode { get; set; }

        public string? ErrorCode { get; set; }

        public string? EntityType { get; set; }

        public string? EntityId { get; set; }
    }
}
