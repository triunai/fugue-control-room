using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Customers;

namespace Fugue.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent development data: 25 customers with stable ids, written with Dapper
/// <c>INSERT ... ON CONFLICT (id) DO UPDATE</c> (the upsert path of ADR-006) inside one unit of work. Running it
/// twice leaves 25 rows. Created-at values are fixed so keyset paging is deterministic.
/// </summary>
internal sealed class CustomerSeeder(SqlExecutor sql, IUnitOfWork unitOfWork, IAuditStamp stamp) : ICustomerSeeder
{
    private const int Count = 25;
    private static readonly DateTimeOffset Epoch = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const string UpsertSql = """
        INSERT INTO customers (id, name, email, phone, status, created_at, created_by, updated_at, updated_by)
        VALUES (@Id, @Name, @Email, @Phone, @Status, @CreatedAt, @Actor, @Now, @Actor)
        ON CONFLICT (id) DO UPDATE SET
          name = EXCLUDED.name, email = EXCLUDED.email, phone = EXCLUDED.phone, status = EXCLUDED.status,
          updated_at = EXCLUDED.updated_at, updated_by = EXCLUDED.updated_by
        """;

    /// <summary>The stable id of seeded customer <paramref name="index"/> (1-based).</summary>
    public static Guid IdFor(int index) => new($"01900000-0000-7000-8000-{index:D12}");

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        AuditStampValue now = stamp.Capture();
        await unitOfWork.BeginAsync(ct);
        for (int i = 1; i <= Count; i++)
        {
            await sql.ExecuteAsync(
                UpsertSql,
                new
                {
                    Id = Domain.Ids.CustomerId.From(IdFor(i)),
                    Name = $"Sample Customer {i:D2}",
                    Email = $"seed.customer{i:D2}@example.test",
                    Phone = i % 3 == 0 ? $"+1415555{i:D4}" : null,
                    Status = (i % 5 == 0 ? CustomerStatus.Suspended : CustomerStatus.Active).ToString(),
                    CreatedAt = Epoch.AddMinutes(i),
                    now.Now,
                    now.Actor,
                },
                ct);
        }

        await unitOfWork.CommitAsync(ct);
        return Count;
    }
}
