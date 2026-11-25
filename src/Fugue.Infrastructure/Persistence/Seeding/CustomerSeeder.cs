using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent development data: 25 customers with stable ids, written with Dapper
/// <c>INSERT ... ON CONFLICT (id) DO UPDATE</c> (the upsert path of ADR-006) inside one unit of work. Running it
/// twice leaves 25 rows, and the second run writes nothing (a row that already matches is skipped). Every row it creates or overwrites gets its
/// own audit row with the old and new values. Created-at values are fixed so keyset paging is deterministic.
/// </summary>
internal sealed class CustomerSeeder(FugueDbContext db, SqlExecutor sql, IUnitOfWork unitOfWork, IAuditStamp stamp, IAuditTrail audit) : ICustomerSeeder
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
        var ids = Enumerable.Range(1, Count).Select(i => Domain.Ids.CustomerId.From(IdFor(i))).ToList();
        Dictionary<Guid, Customer> existing = (await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct))
            .ToDictionary(c => c.Id.Value);

        await unitOfWork.BeginAsync(ct);
        for (int i = 1; i <= Count; i++)
        {
            Guid id = IdFor(i);
            var desired = new Dictionary<string, object?>
            {
                ["Name"] = $"Sample Customer {i:D2}",
                ["Email"] = $"seed.customer{i:D2}@example.test",
                ["Phone"] = i % 3 == 0 ? $"+1415555{i:D4}" : null,
                ["Status"] = (i % 5 == 0 ? CustomerStatus.Suspended : CustomerStatus.Active).ToString(),
            };

            // Per-row audit evidence, and no churn: a row that already matches is neither upserted nor audited.
            Dictionary<string, AuditFieldChange> changes = existing.TryGetValue(id, out Customer? current)
                ? Differences(current, desired)
                : desired.Where(f => f.Value is not null).ToDictionary(f => f.Key, f => AuditFieldChange.Added(f.Value));
            if (changes.Count == 0)
            {
                continue;
            }

            await sql.ExecuteAsync(
                UpsertSql,
                new
                {
                    Id = Domain.Ids.CustomerId.From(id),
                    Name = desired["Name"],
                    Email = desired["Email"],
                    Phone = desired["Phone"],
                    Status = desired["Status"],
                    CreatedAt = Epoch.AddMinutes(i),
                    now.Now,
                    now.Actor,
                },
                ct);
            await audit.RecordAsync(
                new AuditEntry(nameof(Customer), id.ToString(), changes, typeof(Customer), "customers.seed",
                    new Dictionary<string, object?> { ["source"] = "development seed" }),
                ct);
        }

        await unitOfWork.CommitAsync(ct);
        return Count;
    }

    private static Dictionary<string, AuditFieldChange> Differences(Customer current, Dictionary<string, object?> desired)
    {
        var currentValues = new Dictionary<string, object?>
        {
            ["Name"] = current.Name,
            ["Email"] = current.Email,
            ["Phone"] = current.Phone,
            ["Status"] = current.Status.ToString(),
        };
        return desired.Where(f => !Equals(currentValues[f.Key], f.Value)).ToDictionary(f => f.Key, f => AuditFieldChange.Changed(currentValues[f.Key], f.Value));
    }
}
