using Fugue.Domain.Abstractions;
using Fugue.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Fugue.Tests.Persistence;

/// <summary>A deterministic clock the audit tests can move.</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>A caller the audit tests can switch; null means "no HTTP user" (actor "system").</summary>
public sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }

    public Guid? TenantId => null;

    public bool IsAuthenticated => UserId is not null;

    public bool HasPermission(string code) => true;
}

/// <summary>
/// One real Postgres container and one API host for every test in the "postgres" collection; migrations are
/// applied once. Tests isolate themselves with unique emails and search tokens rather than resetting the database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program>? _factory;

    public TestClock Clock { get; } = new();

    public TestCurrentUser User { get; } = new();

    public string ConnectionString => _container.GetConnectionString();

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialised.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", ConnectionString);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.RemoveAll<ICurrentUser>();
                services.AddSingleton<ICurrentUser>(User);
            });
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FugueDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
