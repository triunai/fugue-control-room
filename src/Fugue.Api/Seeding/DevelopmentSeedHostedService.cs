using Fugue.Infrastructure.Identity;
using Fugue.Infrastructure.Persistence.Seeding;

namespace Fugue.Api.Seeding;

/// <summary>
/// Runs the idempotent customer, user and claim seeders once at startup. A hosted service (not a seed-and-exit flag) so
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

        // The claims seed needs the dev users (claimants and officers), so it runs after them.
        LogClaims(await scope.ServiceProvider.GetRequiredService<IClaimSeeder>().SeedAsync(cancellationToken));

        // Claim events land in the dev sink during a demo (runbooks 07 and 11). Delivery to http needs Webhooks:AllowInsecureLocal, which only Development honours.
        string sinkUrl = configuration["Seed:DevSinkUrl"] ?? $"{LocalBase()}/api/v1/dev/webhook-sink";
        LogSink(await scope.ServiceProvider.GetRequiredService<IDevSinkSeeder>().SeedAsync(sinkUrl, cancellationToken), sinkUrl);
    }

    /// <summary>The first configured listen URL with a wildcard host made local, else the default dev port.</summary>
    private string LocalBase()
    {
        string? first = (configuration["urls"] ?? configuration["ASPNETCORE_URLS"])?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return first is null
            ? "http://localhost:5080"
            : first.Replace("://*", "://localhost", StringComparison.Ordinal).Replace("://+", "://localhost", StringComparison.Ordinal).Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal).TrimEnd('/');
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} development customers")]
    private partial void LogSeeded(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded development users (admin, staff, manager, support, self, self2)")]
    private partial void LogUsers();

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} development claims")]
    private partial void LogClaims(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dev webhook sink subscribed to claim.* at {Url} (created: {Created})")]
    private partial void LogSink(bool created, string url);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
