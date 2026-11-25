using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Persistence;

internal static class PersistenceOptions
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
}
