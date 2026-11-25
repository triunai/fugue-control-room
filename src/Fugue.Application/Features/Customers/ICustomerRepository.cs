using FluentResults;
using Fugue.Domain.Customers;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Customers;

/// <summary>
/// Persistence contract for the Customers slice. Data-access split (ADR-006): reads and concurrency-checked
/// updates use EF Core; inserts use Dapper.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>Insert (Dapper). Fails with <c>customer.email_taken</c> (Conflict) on a duplicate email.</summary>
    Task<Result<CustomerDto>> AddAsync(Customer customer, CancellationToken ct);

    /// <summary>Read (EF, no tracking).</summary>
    Task<CustomerDto?> GetAsync(CustomerId id, CancellationToken ct);

    /// <summary>
    /// Keyset page ordered by (created_at, id). Returns up to <paramref name="take"/> rows after
    /// <paramref name="after"/>; the caller asks for one extra row to learn whether another page exists.
    /// </summary>
    Task<IReadOnlyList<CustomerDto>> ListAsync(
        Common.KeysetCursor? after, CustomerStatus? status, string? search, int take, CancellationToken ct);

    /// <summary>Loads a tracked aggregate for a following <see cref="SaveAsync"/> or <see cref="DeleteAsync"/>.</summary>
    Task<Customer?> FindForUpdateAsync(CustomerId id, CancellationToken ct);

    /// <summary>
    /// Saves a tracked change (EF). When <paramref name="expectedVersion"/> is given and differs from the stored
    /// version the result is <c>customer.version_mismatch</c> (PreconditionFailed).
    /// </summary>
    Task<Result<CustomerDto>> SaveAsync(Customer customer, long? expectedVersion, CancellationToken ct);

    Task<Result> DeleteAsync(Customer customer, long expectedVersion, CancellationToken ct);
}
