using Fugue.Infrastructure.Identity;
using Fugue.Infrastructure.Persistence.Seeding;

namespace Fugue.Api.Seeding;

/// <summary>
/// Runs the idempotent customer seeder once at startup. A hosted service (not a seed-and-exit flag) so
/// <c>dotnet run -- --seed</c> leaves a running, populated API. Registered only in Development.
/// The database must already be migrated; a failure is logged and stops startup rather than serving a half-seeded API.
/// </summary>
internal sealed partial class DevelopmentSeedHostedService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<DevelopmentSeedHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        int count = await scope.ServiceProvider.GetRequiredService<ICustomerSeeder>().SeedAsync(cancellationToken);
        LogSeeded(count);

        // Development-only password (documented in docs/runbooks/05-auth.md). Override with Seed:DevPassword.
        string password = configuration["Seed:DevPassword"] ?? "Fugue-Dev-Passw0rd!"; // secrets-scan:ignore
        await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedDevelopmentUsersAsync(password, cancellationToken);
        LogUsers();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} development customers")]
    private partial void LogSeeded(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded development users (admin, staff, support, self, self2)")]
    private partial void LogUsers();

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
