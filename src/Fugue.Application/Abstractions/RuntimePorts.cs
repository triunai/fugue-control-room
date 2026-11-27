namespace Fugue.Application.Abstractions;

/// <summary>
/// One database transaction shared by EF Core and Dapper. Between <see cref="BeginAsync"/> and
/// <see cref="CommitAsync"/>, EF <c>SaveChanges</c> and Dapper statements run on the same connection and transaction,
/// so mixed writes are atomic. Disposing without committing rolls back. This is the seam the S4 outbox uses
/// (business change and outbox row commit together). ADR-006.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken ct);

    Task CommitAsync(CancellationToken ct);
}

/// <summary>
/// Per-request correlation identifiers (ADR-005). <see cref="CorrelationId"/> identifies the request chain;
/// <see cref="SagaId"/> identifies a long-running business process (for example the aggregate id of a
/// workflow) and may be supplied by the caller or set by a handler.
/// </summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }

    string? SagaId { get; }

    /// <summary>Sets the saga id. Throws <see cref="ArgumentException"/> when it is not a valid id (<see cref="CorrelationIdFormat"/>).</summary>
    void SetSaga(string sagaId);
}

/// <summary>
/// The hard module kill switch: configuration <c>Modules:CODE:Enabled</c> (default true), read live so a reload takes effect.
/// A module is effectively enabled only when this AND the <c>modules.is_active</c> row agree (ADR-009).
/// </summary>
public interface IModuleConfig
{
    /// <summary>Codes explicitly switched off in configuration, upper-case.</summary>
    IReadOnlySet<string> DisabledCodes();

    bool IsEnabledInConfig(string code) => !DisabledCodes().Contains(code.ToUpperInvariant());
}
