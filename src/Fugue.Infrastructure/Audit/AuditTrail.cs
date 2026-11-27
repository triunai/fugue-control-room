using Fugue.Application.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Infrastructure.Persistence;

namespace Fugue.Infrastructure.Audit;

/// <summary>
/// The Dapper write path of <c>audit_log</c> (ADR-006 insert path): runs through <see cref="SqlExecutor"/>, so inside a unit of work the
/// row joins that transaction. Used for Dapper writes (Customer insert, seeders); the failure row is the exception: it is written on its own connection.
/// </summary>
internal sealed class AuditTrail(SqlExecutor sql, AuditRowFactory factory, AuditRedactor redactor) : IAuditTrail
{
    private static readonly string InsertSql = SqlScripts.Get("audit/insert-audit-log");

    public async Task RecordAsync(AuditEntry entry, CancellationToken ct)
    {
        if (!factory.Enabled)
        {
            return;
        }

        string? changes = redactor.Build(entry.EntityClrType, entry.Changes);
        AuditLog row = factory.Create(
            entry.DefaultAction, $"{entry.EntityType.ToLowerInvariant()}.write", entry.EntityType, entry.EntityId, AuditOutcomes.Success, null, changes, entry.Metadata);
        await sql.ExecuteAsync(InsertSql, Parameters(row), ct); // joins the caller's transaction: commits or rolls back with the change
    }

    public async Task RecordFailureAsync(AuditFailure failure, CancellationToken ct)
    {
        if (!factory.Enabled)
        {
            return;
        }

        AuditLog row = factory.Create(
            failure.Action, failure.Action, failure.EntityType, failure.EntityId, AuditOutcomes.Failure, failure.ErrorCode, null, failure.Metadata,
            failure.ErrorType, failure.ErrorMessage, failure.DurationMs);

        // Own connection and transaction: a failure row must survive the rollback of a surrounding unit of work, and an aborted
        // surrounding transaction (a unique violation) must not stop it being written.
        await sql.ExecuteIndependentAsync(InsertSql, Parameters(row), ct);
    }

    private static object Parameters(AuditLog row) => new
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
        row.ErrorType,
        row.ErrorMessage,
        row.DurationMs,
        row.Changes,
        row.CorrelationId,
        row.SagaId,
        row.TraceId,
        row.HttpMethod,
        row.RouteTemplate,
        row.ClientIp,
        row.UserAgent,
        row.Metadata,
    };
}
