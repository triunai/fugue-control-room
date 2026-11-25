using Fugue.Domain.Customers;
using Fugue.Infrastructure.Persistence.Ids;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Persistence;

public sealed class FugueDbContext(DbContextOptions<FugueDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FugueDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        TypedIds.ConfigureEfConventions(configurationBuilder);
}
