using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fugue.Infrastructure.Persistence;

/// <summary>
/// Dapper on EF's own connection and current transaction (ADR-006). Every Dapper write goes through here, so a
/// statement inside <c>IUnitOfWork</c> is atomic with EF changes, and outside one it simply autocommits.
/// </summary>
internal sealed class SqlExecutor(FugueDbContext db)
{
    public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct) =>
        RunAsync((connection, transaction) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct)), ct);

    public Task<T> ExecuteScalarAsync<T>(string sql, object? parameters, CancellationToken ct) =>
        RunAsync(async (connection, transaction) =>
            (await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct)))!, ct);

    /// <summary>
    /// Runs one statement on its OWN connection and transaction, deliberately outside any unit of work. For rows that must survive the
    /// rollback of the caller's transaction (the failure audit row) and must not be poisoned by an aborted one.
    /// </summary>
    public async Task<int> ExecuteIndependentAsync(string sql, object? parameters, CancellationToken ct)
    {
        string connectionString = db.Database.GetConnectionString() ?? throw new InvalidOperationException("The database has no connection string.");
        await using var connection = new Npgsql.NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    private async Task<T> RunAsync<T>(Func<System.Data.Common.DbConnection, System.Data.Common.DbTransaction?, Task<T>> action, CancellationToken ct)
    {
        System.Data.Common.DbConnection connection = db.Database.GetDbConnection();
        IDbContextTransaction? current = db.Database.CurrentTransaction;
        await db.Database.OpenConnectionAsync(ct); // reference-counted by EF; a no-op inside a transaction
        try
        {
            return await action(connection, current?.GetDbTransaction());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
