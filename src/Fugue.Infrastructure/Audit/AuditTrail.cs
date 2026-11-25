using Fugue.Application.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Infrastructure.Persistence;

namespace Fugue.Infrastructure.Audit;

/// <summary>
/// The Dapper write path of <c>audit_log</c> (ADR-006 insert path): runs through <see cref="SqlExecutor"/>, so inside a unit of work the
/// row joins that transaction. Used for Dapper writes (Customer insert, seeders) and for the single autocommitted failure row.
/// </summary>
internal sealed class AuditTrail(SqlExecutor sql, AuditRowFactory factory, AuditRedactor redactor) : IAuditTrail
{
    private const string InsertSql = """
        INSERT INTO audit_log (id, occurred_at, actor_user_id, actor_type, actor_display, tenant_id, permission_used, action, entity_type, entity_id,
                               outcome, error_code, changes, correlation_id, saga_id, trace_id, http_method, route_template, client_ip, user_agent, metadata)
        VALUES (@Id, @OccurredAt, @ActorUserId, @ActorType, @ActorDisplay, @TenantId, @PermissionUsed, @Action, @EntityType, @EntityId,
                @Outcome, @ErrorCode, CAST(@Changes AS jsonb), @CorrelationId, @SagaId, @TraceId, @HttpMethod, @RouteTemplate, @ClientIp, @UserAgent, CAST(@Metadata AS jsonb))
        """;

    public async Task RecordAsync(AuditEntry entry, CancellationToken ct)
    {
        if (!factory.Enabled)
        {
            return;
        }

        string? changes = redactor.Build(entry.EntityClrType, entry.Changes);
        AuditLog row = factory.Create(
            entry.DefaultAction, $"{entry.EntityType.ToLowerInvariant()}.write", entry.EntityType, entry.EntityId, AuditOutcomes.Success, null, changes, entry.Metadata);
        await InsertAsync(row, ct);
    }

    public async Task RecordFailureAsync(AuditFailure failure, CancellationToken ct)
    {
        if (!factory.Enabled)
        {
            return;
        }

        AuditLog row = factory.Create(
            failure.Action, failure.Action, failure.EntityType, failure.EntityId, AuditOutcomes.Failure, failure.ErrorCode, null, failure.Metadata);
        await InsertAsync(row, ct);
    }

    private Task<int> InsertAsync(AuditLog row, CancellationToken ct) =>
        sql.ExecuteAsync(
            InsertSql,
            new
            {
                row.Id,
                row.OccurredAt,
                row.ActorUserId,
                row.ActorType,
                row.ActorDisplay,
                row.TenantId,
                row.PermissionUsed,
                row.Action,
                row.EntityType,
                row.EntityId,
                row.Outcome,
                row.ErrorCode,
                row.Changes,
                row.CorrelationId,
                row.SagaId,
                row.TraceId,
                row.HttpMethod,
                row.RouteTemplate,
                row.ClientIp,
                row.UserAgent,
                row.Metadata,
            },
            ct);
}
