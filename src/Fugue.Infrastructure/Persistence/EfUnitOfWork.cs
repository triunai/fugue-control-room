using Fugue.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fugue.Infrastructure.Persistence;

/// <summary>
/// One transaction on the EF connection. Dapper statements issued through <see cref="SqlExecutor"/> enlist in it
/// (<c>CurrentTransaction.GetDbTransaction()</c>), as does <c>SaveChanges</c>. ADR-006.
/// </summary>
internal sealed class EfUnitOfWork(FugueDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task BeginAsync(CancellationToken ct)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A unit of work is already in progress.");
        }

        _transaction = await db.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("BeginAsync was not called.");
        }

        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync(); // uncommitted: rolls back
            _transaction = null;
        }
    }
}
