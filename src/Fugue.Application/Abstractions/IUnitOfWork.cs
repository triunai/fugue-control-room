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
