using Fugue.Application.Abstractions;
using Fugue.Application.Features.Customers;
using Fugue.Domain.Abstractions;
using Fugue.Infrastructure.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Infrastructure.Persistence.Customers;
using Fugue.Infrastructure.Persistence.Ids;
using Fugue.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fugue.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers EF Core, Dapper support, audit stamping, the unit of work and repositories (ADR-006).</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        TypedIds.RegisterDapperHandlers();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAuditStamp, AuditStamp>();
        services.AddScoped<AuditableSaveChangesInterceptor>();

        services.AddDbContext<FugueDbContext>((sp, options) =>
        {
            string connection = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:Postgres is not configured. Set ConnectionStrings__Postgres (see .env.example) or use dotnet user-secrets.");
            PersistenceOptions.Configure(options, connection);
            options.AddInterceptors(sp.GetRequiredService<AuditableSaveChangesInterceptor>());
        });

        services.AddScoped<SqlExecutor>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerSeeder, CustomerSeeder>();

        services.Configure<PasswordOptions>(o =>
        {
            if (int.TryParse(configuration["Auth:PasswordHashIterations"], out int iterations) && iterations > 0)
            {
                o.PasswordHashIterations = iterations;
            }
        });
        services.AddSingleton<PasswordVerifier>();
        services.AddSingleton<IPasswordVerifier>(sp => sp.GetRequiredService<PasswordVerifier>());
        services.AddSingleton<IModuleConfig, ConfigModuleSwitch>();
        services.AddScoped<IUserAccessReader, UserAccessReader>();
        services.AddScoped<IAuthStore, AuthStore>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IAccessAuditWriter, AccessAuditWriter>();
        services.AddScoped<Fugue.Application.Features.Modules.IModuleRegistry, ModuleRegistry>();
        services.AddScoped<Fugue.Application.Features.Groups.IGroupAdmin, GroupAdmin>();
        services.AddScoped<Fugue.Application.Features.Overrides.IOverrideAdmin, OverrideAdmin>();
        services.AddScoped<IGrantAuditWriter, GrantAuditWriter>();
        services.AddScoped<IIdentitySeeder, IdentitySeeder>();
        return services;
    }
}
