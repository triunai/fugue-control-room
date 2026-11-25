using System.Net.Http.Json;
using Fugue.Api.Auth;
using Fugue.Application.Features.Auth;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure;
using Fugue.Infrastructure.Identity;
using Fugue.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

/// <summary>
/// The ambient caller. Over HTTP it is the real authenticated principal (so tests exercise real tokens and permissions);
/// outside HTTP (mediator seam tests) it is "system" with every permission.
/// </summary>
public sealed class TestCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private readonly ClaimsCurrentUser _claims = new(accessor);

    public Guid? UserId => _claims.IsAuthenticated ? _claims.UserId : null;

    public Guid? TenantId => _claims.IsAuthenticated ? _claims.TenantId : null;

    public bool IsAuthenticated => _claims.IsAuthenticated;

    public bool HasPermission(string code) => !_claims.IsAuthenticated || _claims.HasPermission(code);
}

/// <summary>
/// One real Postgres container and one API host for every test in the "postgres" collection; migrations are
/// applied once and the development users are seeded once. Tests isolate themselves with unique emails and search
/// tokens rather than resetting the database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly Dictionary<string, (string Token, DateTimeOffset IssuedAt)> _tokens = [];
    private WebApplicationFactory<Program>? _factory;

    public TestClock Clock { get; } = new();

    public string SigningKey { get; } = TestAuth.NewKey();

    public string ConnectionString => _container.GetConnectionString();

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialised.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", ConnectionString);
            TestAuth.Apply(builder, SigningKey);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.RemoveAll<ICurrentUser>();
                services.AddSingleton<ICurrentUser>(sp => new TestCurrentUser(sp.GetRequiredService<IHttpContextAccessor>()));
            });
        });

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<FugueDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Fugue.Infrastructure.Persistence.Seeding.ICustomerSeeder>().SeedAsync(default);
        await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedDevelopmentUsersAsync(TestAuth.Password, default);
    }

    /// <summary>A client that signs in as <paramref name="email"/> (re-signing in when the test clock has moved past the token).</summary>
    public HttpClient ClientAs(string email) => Factory.CreateDefaultClient(new BearerHandler(this, email));

    /// <summary>A client with no credentials.</summary>
    public HttpClient Anonymous() => Factory.CreateClient();

    public async Task<string> TokenAsync(string email)
    {
        DateTimeOffset now = Clock.GetUtcNow();
        if (_tokens.TryGetValue(email, out (string Token, DateTimeOffset IssuedAt) cached)
            && now >= cached.IssuedAt && now < cached.IssuedAt.AddMinutes(10))
        {
            return cached.Token;
        }

        using HttpClient plain = Anonymous();
        HttpResponseMessage response = await plain.PostAsJsonAsync("/api/v1/auth/token", new { email, password = TestAuth.Password });
        response.EnsureSuccessStatusCode();
        string token = (await response.Content.ReadFromJsonAsync<TokenDto>())!.AccessToken;
        _tokens[email] = (token, now);
        return token;
    }

    /// <summary>Creates a user (password <see cref="TestAuth.Password"/>) holding the given roles and permission overrides.</summary>
    public async Task<(string Email, UserId Id)> NewUserAsync(string[]? roles = null, (string Permission, bool Allow, DateTimeOffset? Expires)[]? overrides = null)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        FugueDbContext context = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        string email = $"u{Guid.NewGuid():N}@example.test";
        var user = new User
        {
            Id = UserId.New(),
            DisplayName = "Test user",
            Email = email,
            PasswordHash = scope.ServiceProvider.GetRequiredService<PasswordVerifier>().Hash(TestAuth.Password),
        };
        context.Users.Add(user);
        foreach (string role in roles ?? [])
        {
            context.UserRoles.Add(new UserRole { Id = UserRoleId.New(), UserId = user.Id, RoleId = IdentityCatalog.RoleIdFor(role) });
        }

        foreach ((string permission, bool allow, DateTimeOffset? expires) in overrides ?? [])
        {
            context.Overrides.Add(new UserPermissionOverride
            {
                Id = UserPermissionOverrideId.New(),
                UserId = user.Id,
                PermissionId = IdentityCatalog.PermissionIdFor(permission),
                IsAllowed = allow,
                ExpiresAt = expires,
            });
        }

        await context.SaveChangesAsync();
        return (email, user.Id);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    private sealed class BearerHandler(PostgresFixture fixture, string email) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await fixture.TokenAsync(email));
            return await base.SendAsync(request, cancellationToken);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
