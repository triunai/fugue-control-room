using System.Linq.Expressions;
using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Common;
using Fugue.Application.Features.Customers.Helpers;
using Fugue.Application.Features.Customers.Models;
using Fugue.Application.Features.Customers.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Customers;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Fugue.Infrastructure.Persistence.Customers;

/// <summary>
/// ADR-006: Dapper inserts, EF reads (AsNoTracking projections) and EF tracked updates guarded by the xmin
/// concurrency token. Unique-violation (SQLSTATE 23505) on the per-owner email indexes becomes <c>customer.email_taken</c>.
/// </summary>
internal sealed class CustomerRepository(FugueDbContext db, SqlExecutor sql, IAuditStamp stamp, IAuditTrail audit, IOutbox outbox) : ICustomerRepository
{
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";

    private static readonly string InsertSql = SqlScripts.Get("customers/insert");

    public async Task<Result<CustomerDto>> AddAsync(Customer customer, CancellationToken ct)
    {
        AuditStampValue now = stamp.Capture();
        customer.CreatedAt = customer.UpdatedAt = now.Now;
        customer.CreatedBy = customer.UpdatedBy = now.Actor;

        // The insert and its audit row are one transaction: join the caller's unit of work, or open a short one. A failed insert
        // leaves nothing behind (the finally rolls back before the caller writes its own failure audit row on the same connection).
        IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            long version = await sql.ExecuteScalarAsync<long>(
                InsertSql,
                new
                {
                    customer.Id,
                    customer.Name,
                    customer.Email,
                    customer.Phone,
                    Status = customer.Status.ToString(),
                    customer.UserId,
                    now.Now,
                    now.Actor,
                },
                ct);
            await audit.RecordAsync(AuditSnapshot.Created(customer, customer.Id.ToString(), "customers.create"), ct);
            await outbox.EnqueueAsync(customer, ct); // same transaction: the event exists if and only if the insert commits (ADR-011)
            if (own is not null)
            {
                await own.CommitAsync(ct);
            }

            return Result.Ok(ToDto(customer, version));
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            return Result.Fail<CustomerDto>(CustomerErrors.EmailTaken());
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolation)
        {
            return Result.Fail<CustomerDto>(CustomerErrors.OwnerNotFound()); // an owner id that is not a user
        }
        finally
        {
            if (own is not null)
            {
                await own.DisposeAsync(); // not committed: rolls back
            }
        }
    }

    public async Task<CustomerDto?> GetAsync(CustomerId id, OwnerScope scope, CancellationToken ct) =>
        (await Scoped(db.Customers.AsNoTracking(), scope).Where(c => c.Id == id).Select(Projection).FirstOrDefaultAsync(ct))?.ToDto();

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(
        KeysetCursor? after, CustomerStatus? status, string? search, int take, OwnerScope scope, CancellationToken ct)
    {
        IQueryable<Customer> query = after is { } cursor
            // Row-value comparison is not expressible in LINQ; the rest of the query composes on top of this.
            ? db.Customers.FromSqlRaw(SqlScripts.Get("customers/list-after"), new NpgsqlParameter("createdAt", cursor.CreatedAt), new NpgsqlParameter("id", cursor.Id))
            : db.Customers;

        query = Scoped(query.AsNoTracking(), scope);
        if (status is { } s)
        {
            query = query.Where(c => c.Status == s);
        }

        if (!string.IsNullOrEmpty(search))
        {
            string pattern = $"%{EscapeLike(search)}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern, "\\") || EF.Functions.ILike(c.Email, pattern, "\\"));
        }

        List<Row> rows = await query.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).Take(take).Select(Projection).ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public Task<Customer?> FindForUpdateAsync(CustomerId id, OwnerScope scope, CancellationToken ct) =>
        Scoped(db.Customers, scope).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Result<CustomerDto>> SaveAsync(Customer customer, long? expectedVersion, CancellationToken ct)
    {
        if (!MatchesExpected(customer, expectedVersion))
        {
            return Result.Fail<CustomerDto>(CustomerErrors.VersionMismatch());
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail<CustomerDto>(CustomerErrors.VersionMismatch());
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            return Result.Fail<CustomerDto>(CustomerErrors.EmailTaken());
        }

        return Result.Ok(ToDto(customer, customer.RowVersion));
    }

    public async Task<Result> DeleteAsync(Customer customer, long expectedVersion, CancellationToken ct)
    {
        if (!MatchesExpected(customer, expectedVersion))
        {
            return Result.Fail(CustomerErrors.VersionMismatch());
        }

        db.Customers.Remove(customer);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail(CustomerErrors.VersionMismatch());
        }

        return Result.Ok();
    }

    /// <summary>
    /// Compares the loaded version with If-Match, and pins it as the EF original value so a write that raced in
    /// between the load and the save also fails (the UPDATE/DELETE carries <c>WHERE xmin = @expected</c>).
    /// </summary>
    private bool MatchesExpected(Customer customer, long? expectedVersion)
    {
        if (expectedVersion is not { } expected)
        {
            return true;
        }

        if (expected != customer.RowVersion)
        {
            return false;
        }

        db.Entry(customer).Property(c => c.RowVersion).OriginalValue = (uint)expected;
        return true;
    }

    // xid has no cast to bigint in SQL, so the projection keeps the raw uint and widens it in memory.
    /// <summary>
    /// The ownership filter, applied to every read and find-for-update (so keyset pages are filtered too).
    /// A caller with no owner id and no <c>CUSTOMERS_ALL</c> matches nothing; <c>user_id IS NULL</c> rows are never theirs.
    /// </summary>
    private static IQueryable<Customer> Scoped(IQueryable<Customer> query, OwnerScope scope) =>
        scope.All ? query
        : scope.OwnerId is { } owner ? query.Where(c => c.UserId == owner)
        : query.Where(_ => false);

    private static readonly Expression<Func<Customer, Row>> Projection = c => new Row(
        c.Id, c.Name, c.Email, c.Phone, c.Status, c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy, c.RowVersion, c.UserId);

    private sealed record Row(
        CustomerId Id, string Name, string Email, string? Phone, CustomerStatus Status,
        DateTimeOffset CreatedAt, string CreatedBy, DateTimeOffset UpdatedAt, string UpdatedBy, uint RowVersion, UserId? OwnerId)
    {
        public CustomerDto ToDto() => new(Id, Name, Email, Phone, Status, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion, OwnerId);
    }

    private static CustomerDto ToDto(Customer c, long version) => new(
        c.Id, c.Name, c.Email, c.Phone, c.Status, c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy, version, c.UserId);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
