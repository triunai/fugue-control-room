using Fugue.Domain.Audit;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Search;

/// <summary>Read side for the indexer: <c>audit_log</c> oldest first, keyset after a <c>(occurred_at, id)</c> checkpoint, no tracking.</summary>
internal sealed class AuditIndexSource(FugueDbContext db)
{
    public async Task<IReadOnlyList<AuditLog>> NextBatchAsync(DateTimeOffset? afterOccurredAt, Guid? afterId, int take, CancellationToken ct)
    {
        IQueryable<AuditLog> query = afterOccurredAt is { } at && afterId is { } id
            ? db.AuditLogs.FromSqlRaw(SqlScripts.Get("audit/index-batch"), new NpgsqlParameter("occurredAt", at), new NpgsqlParameter("id", id))
            : db.AuditLogs;

        return await query.AsNoTracking().OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).Take(take).ToListAsync(ct);
    }
}
