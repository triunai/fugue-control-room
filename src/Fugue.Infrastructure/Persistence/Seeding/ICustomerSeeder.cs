namespace Fugue.Infrastructure.Persistence.Seeding;

public interface ICustomerSeeder
{
    /// <summary>Upserts the development customers. Returns how many were written.</summary>
    Task<int> SeedAsync(CancellationToken ct);
}
