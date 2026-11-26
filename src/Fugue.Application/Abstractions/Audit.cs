namespace Fugue.Application.Abstractions;

/// <summary>One field of an audit row: both values, only the new one (added), or only the old one (deleted).</summary>
public sealed record AuditFieldChange(object? Old, object? New, bool HasOld, bool HasNew)
{
    public static AuditFieldChange Added(object? value) => new(null, value, false, true);

    public static AuditFieldChange Removed(object? value) => new(value, null, true, false);

    public static AuditFieldChange Changed(object? oldValue, object? newValue) => new(oldValue, newValue, true, true);
}

/// <summary>
/// An explicit audit entry for a write the EF change tracker cannot see (a Dapper insert, upsert or set-based statement).
/// <paramref name="EntityClrType"/>, when given, lets the redactor honour <c>[AuditMask]</c> and <c>[NoAudit]</c> on the entity's properties.
/// <paramref name="DefaultAction"/> is used only when no command is in scope (a seeder, a tool); inside a command the command's action wins. Field names are CLR property names (PascalCase).
/// </summary>
public sealed record AuditEntry(
    string EntityType,
    string? EntityId,
    IReadOnlyDictionary<string, AuditFieldChange> Changes,
    Type? EntityClrType = null,
    string? DefaultAction = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);

/// <summary>A command that did not change anything because it failed: written as ONE <c>outcome = failure</c> row.</summary>
public sealed record AuditFailure(string Action, string? EntityType, string? EntityId, string ErrorCode, IReadOnlyDictionary<string, object?>? Metadata);

/// <summary>
/// Writes <c>audit_log</c> rows that EF's change tracker cannot produce (ADR-010). Successful EF writes are captured automatically
/// by the SaveChanges interceptor in the same SaveChanges; this port is for Dapper paths and failures.
/// <see cref="RecordAsync"/> runs on EF's connection and current transaction (<c>SqlExecutor</c>), so inside an <see cref="IUnitOfWork"/>
/// the row commits or rolls back with the change. <see cref="RecordFailureAsync"/> is the exception: one insert on its own connection and transaction, so it survives the rollback of a surrounding unit of work.
/// </summary>
public interface IAuditTrail
{
    Task RecordAsync(AuditEntry entry, CancellationToken ct);

    Task RecordFailureAsync(AuditFailure failure, CancellationToken ct);
}

/// <summary>
/// The ambient audit context of the command being handled. <c>AuditBehavior</c> sets it; the
/// interceptor and <see cref="IAuditTrail"/> read it, so every row of one command carries the same action. Empty outside a command.
/// </summary>
public interface IAuditScope
{
    string? Action { get; }

    string? ActorDisplay { get; }

    IReadOnlyDictionary<string, object?> Metadata { get; }

    void Begin(string action, string? actorDisplay, IReadOnlyDictionary<string, object?> metadata);

    void End();
}

/// <summary>What the HTTP request adds to an audit row. All members are null outside a request (seeders, background work).</summary>
public sealed record AuditRequestInfo(string? HttpMethod, string? RouteTemplate, string? ClientIp, string? UserAgent, string? PermissionUsed)
{
    public static readonly AuditRequestInfo None = new(null, null, null, null, null);
}

/// <summary>Supplied by the web host so Application and Infrastructure never touch ASP.NET Core.</summary>
public interface IAuditRequestInfo
{
    AuditRequestInfo Current { get; }
}

/// <summary>Resolves the display name stored on audit rows (a snapshot taken when the command runs).</summary>
public interface IActorDirectory
{
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct);
}
