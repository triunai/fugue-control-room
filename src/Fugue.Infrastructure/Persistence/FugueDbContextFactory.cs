using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fugue.Infrastructure.Persistence;

/// <summary>
/// Design-time only (<c>dotnet ef</c>). Reads <c>ConnectionStrings__Postgres</c> from the environment. Without it a
/// password-less local string is used, which is enough for <c>migrations add</c> but not for <c>database update</c>.
/// </summary>
internal sealed class FugueDbContextFactory : IDesignTimeDbContextFactory<FugueDbContext>
{
    public FugueDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=fugue;Username=fugue";
        DbContextOptionsBuilder<FugueDbContext> options = new();
        PersistenceOptions.Configure(options, connection);
        return new FugueDbContext(options.Options);
    }
}
