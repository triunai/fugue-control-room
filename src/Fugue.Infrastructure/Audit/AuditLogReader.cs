using System.Text.Json;
using Fugue.Application.Common;
using Fugue.Application.Features.Audit;
using Fugue.Domain.Audit;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Audit;

/// <summary>
/// EF read side of <c>audit_log</c>: a no-tracking projection, filters as parameterized exact matches (never string-built SQL), keyset
/// paging with a row-value comparison on <c>(occurred_at, id)</c> that the <c>ix_audit_log_*</c> indexes serve. Newest first.
/// </summary>
internal sealed class AuditLogReader(FugueDbContext db) : IAuditLogReader
{
    public async Task<IReadOnlyList<AuditLogDto>> ListAsync(AuditLogFilter filter, KeysetCursor? after, int take, CancellationToken ct)
    {
        IQueryable<AuditLog> query = after is { } cursor
            // Row-value comparison is not expressible in LINQ; the filters compose on top of this.
            ? db.AuditLogs.FromSql($"SELECT * FROM audit_log WHERE (occurred_at, id) < ({cursor.CreatedAt}, {cursor.Id})")
            : db.AuditLogs;

        query = query.AsNoTracking();
        if (filter.EntityType is { } entityType)
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        if (filter.ActorUserId is { } actor)
        {
            query = query.Where(a => a.ActorUserId == actor);
        }

        if (filter.Action is { } action)
        {
            query = query.Where(a => a.Action == action);
        }

        if (filter.CorrelationId is { } correlation)
        {
            query = query.Where(a => a.CorrelationId == correlation);
        }

        if (filter.From is { } from)
        {
            query = query.Where(a => a.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(a => a.OccurredAt < to);
        }

        List<AuditLog> rows = await query.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(take).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    private static AuditLogDto ToDto(AuditLog a) => new(
        a.Id.Value, a.OccurredAt, a.ActorUserId, a.ActorType, a.ActorDisplay, a.TenantId, a.PermissionUsed, a.Action, a.EntityType, a.EntityId,
        a.Outcome, a.ErrorCode, a.ErrorType, a.ErrorMessage, a.DurationMs, Parse(a.Changes), a.CorrelationId, a.SagaId, a.TraceId, a.HttpMethod, a.RouteTemplate, a.ClientIp, a.UserAgent, Parse(a.Metadata));

    private static JsonElement? Parse(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
