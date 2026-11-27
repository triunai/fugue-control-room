namespace Fugue.Infrastructure.Persistence.Seeding;

public interface IClaimSeeder
{
    /// <summary>Writes the development claims that are not there yet (the dev users must exist). Returns how many were written; a second run writes none.</summary>
    Task<int> SeedAsync(CancellationToken ct);
}
