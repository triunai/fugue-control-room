using Fugue.Application.Abstractions;
using Fugue.Application.Features.Claims;
using Fugue.Application.Features.Customers;
using Fugue.Domain.Abstractions;
using Fugue.Infrastructure.Audit;
using Fugue.Infrastructure.Idempotency;
using Fugue.Infrastructure.Identity;
using Fugue.Infrastructure.Messaging;
using Fugue.Infrastructure.Outbox;
using Fugue.Infrastructure.Persistence;
using Fugue.Infrastructure.Persistence.Claims;
using Fugue.Infrastructure.Persistence.Customers;
using Fugue.Infrastructure.Persistence.Ids;
using Fugue.Infrastructure.Persistence.Seeding;
using Fugue.Infrastructure.Search;
using Fugue.Infrastructure.Webhooks;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

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

        // audit_log (ADR-010): options, redaction, the SaveChanges interceptor (EF path) and IAuditTrail (Dapper path and failures).
        services.AddOptions<AuditOptions>(); // bound to the Audit: section by the composition root
        services.AddSingleton<AuditRedactor>();
        services.AddScoped<AuditRowFactory>();
        services.AddScoped<AuditLogSaveChangesInterceptor>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<Fugue.Application.Features.Audit.IAuditLogReader, AuditLogReader>();
        services.AddScoped<Fugue.Application.Features.Audit.IAuditTraceReader, AuditTraceReader>();
        services.AddScoped<IActorDirectory, ActorDirectory>();
        services.TryAddScoped<IAuditRequestInfo, NullAuditRequestInfo>();

        // Audit search (D-058): optional. With no Search:Meilisearch:Url the port answers search.unavailable and no indexer runs.
        services.AddOptions<MeilisearchOptions>().Bind(configuration.GetSection(MeilisearchOptions.Section));
        if (configuration.GetSection(MeilisearchOptions.Section).Get<MeilisearchOptions>()?.Enabled == true)
        {
            services.AddSingleton<MeilisearchAuditSearch>();
            services.AddSingleton<Fugue.Application.Features.Audit.IAuditSearch>(sp => sp.GetRequiredService<MeilisearchAuditSearch>());
            services.AddScoped<AuditIndexSource>();
            services.AddSingleton<AuditIndexer>();
            services.AddHostedService(sp => sp.GetRequiredService<AuditIndexer>());

            // Claim search (D-063): same engine and options, its own indexes and indexer.
            services.AddSingleton<MeilisearchClaimSearch>();
            services.AddSingleton<Fugue.Application.Features.Claims.IClaimSearch>(sp => sp.GetRequiredService<MeilisearchClaimSearch>());
            services.AddScoped<ClaimIndexSource>();
            services.AddSingleton<ClaimIndexer>();
            services.AddHostedService(sp => sp.GetRequiredService<ClaimIndexer>());
        }
        else
        {
            services.AddSingleton<Fugue.Application.Features.Audit.IAuditSearch, DisabledAuditSearch>();
            services.AddSingleton<Fugue.Application.Features.Claims.IClaimSearch, DisabledClaimSearch>();
        }

        // Outbox (ADR-011): events raised by aggregates become outbox rows in the same SaveChanges (EF path) or through IOutbox (Dapper path).
        services.AddScoped<OutboxPayloadFactory>();
        services.AddScoped<OutboxRowFactory>();
        services.AddScoped<OutboxSaveChangesInterceptor>();
        services.AddScoped<IOutbox, OutboxWriter>();

        // Webhooks (ADR-011): options, the SSRF guard, subscription secrets in Data Protection (key ring in the database) and the subscription store.
        services.AddOptions<WebhookOptions>().Bind(configuration.GetSection(WebhookOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<WebhookOptions>, WebhookOptionsValidator>();
        services.AddSingleton<IHostResolver, DnsHostResolver>();
        services.AddSingleton<Fugue.Application.Features.Webhooks.IWebhookUrlGuard, WebhookUrlGuard>();
        services.AddDataProtection().Services.Configure<Microsoft.AspNetCore.DataProtection.DataProtectionOptions>(o => o.ApplicationDiscriminator = "Fugue");
        services.AddOptions<KeyManagementOptions>().Configure<IServiceScopeFactory>((options, scopes) => options.XmlRepository = new DatabaseXmlRepository(scopes));
        services.AddSingleton<Fugue.Application.Features.Webhooks.ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<Fugue.Application.Features.Webhooks.IWebhookSubscriptionStore, WebhookSubscriptionStore>();
        services.AddScoped<Fugue.Application.Features.Webhooks.IWebhookDeliveryStore, WebhookDeliveryStore>();
        services.AddSingleton<Fugue.Application.Features.Webhooks.Sink.IDevWebhookSink, DevWebhookSink>();

        // Idempotency keys (ADR-013): options, the store (Dapper claim, responses encrypted with the same Data Protection ring) and the expiry purge.
        services.AddOptions<IdempotencyOptions>().Bind(configuration.GetSection(IdempotencyOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<IdempotencyOptions>, IdempotencyOptionsValidator>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddHostedService<IdempotencyPurgeService>();

        // Dispatcher (ADR-011): the relay hands outbox messages to IEventPublisher adapters (webhook fan-out first); deliveries go out through a typed client
        // whose primary handler dials only verified addresses (SSRF, DNS rebinding) and never follows redirects. The standard resilience handler adds an
        // attempt timeout, a total timeout, a rate limiter and a circuit breaker PER AUTHORITY (one failing receiver does not open the circuit for the others).
        // Its retry is limited to one attempt and disabled for POST: the outbox is the retry authority (counted, backed off for minutes, visible in webhook_deliveries).
        // Events:Publisher picks the adapters the relay calls: Webhooks (default, unchanged), Kafka, or Both (ADR-011 amendment).
        services.AddOptions<EventPublisherOptions>().Bind(configuration.GetSection(EventPublisherOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<EventPublisherOptions>, EventPublisherOptionsValidator>();
        EventPublisherMode mode = configuration.GetSection(EventPublisherOptions.Section).Get<EventPublisherOptions>()?.Publisher ?? EventPublisherMode.Webhooks;
        if (mode is EventPublisherMode.Webhooks or EventPublisherMode.Both)
        {
            services.AddScoped<Fugue.Application.Abstractions.IEventPublisher, WebhookFanOutPublisher>();
        }

        if (mode is EventPublisherMode.Kafka or EventPublisherMode.Both)
        {
            services.AddSingleton<Confluent.Kafka.IProducer<string, string>>(sp =>
                new Confluent.Kafka.ProducerBuilder<string, string>(KafkaEventPublisher.ProducerConfig(sp.GetRequiredService<IOptions<EventPublisherOptions>>().Value.Kafka)).Build());
            services.AddScoped<Fugue.Application.Abstractions.IEventPublisher, KafkaEventPublisher>();
        }

        services.AddSingleton<GuardedConnect>();
        IHttpStandardResiliencePipelineBuilder pipeline = services.AddHttpClient<WebhookSender>(client => client.Timeout = Timeout.InfiniteTimeSpan) // the resilience handler owns the timeouts
            .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false, // a proxy would receive the connection instead of the verified address
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                PooledConnectionLifetime = TimeSpan.FromMinutes(2), // re-resolve DNS regularly
                ConnectCallback = sp.GetRequiredService<GuardedConnect>().ConnectAsync,
            })
            .AddStandardResilienceHandler();
        services.AddOptions<HttpStandardResilienceOptions>(pipeline.PipelineName).Configure<IOptions<WebhookOptions>>((resilience, webhooks) =>
        {
            TimeSpan attempt = TimeSpan.FromSeconds(webhooks.Value.AttemptTimeoutSeconds);
            resilience.AttemptTimeout.Timeout = attempt;
            resilience.TotalRequestTimeout.Timeout = attempt * 2;
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(30, (webhooks.Value.AttemptTimeoutSeconds * 2) + 1));
            resilience.Retry.MaxRetryAttempts = 1;
            resilience.Retry.ShouldHandle = _ => ValueTask.FromResult(false); // no in-memory retry at all: the outbox retries (counted, backed off)
        });
        pipeline.SelectPipelineByAuthority();
        services.AddSingleton<OutboxDispatcher>();
        if (configuration.GetValue("Webhooks:Dispatcher:Enabled", true))
        {
            services.AddHostedService<OutboxDispatcherService>();
        }

        services.AddDbContext<FugueDbContext>((sp, options) =>
        {
            string connection = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:Postgres is not configured. Set ConnectionStrings__Postgres (see .env.example) or use dotnet user-secrets.");
            PersistenceOptions.Configure(options, connection);
            // Stamp first, then audit, then outbox: the audit diff skips the stamp columns the first interceptor just wrote.
            options.AddInterceptors(
                sp.GetRequiredService<AuditableSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditLogSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        services.AddScoped<SqlExecutor>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerSeeder, CustomerSeeder>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IClaimReports, ClaimReports>();
        services.AddScoped<IClaimSeeder, ClaimSeeder>();
        services.AddScoped<IDevSinkSeeder, DevSinkSeeder>();

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
        services.AddScoped<Fugue.Application.Features.Me.IMeReader, MeReader>();
        services.AddScoped<IAuthStore, AuthStore>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IAccessAuditWriter, AccessAuditWriter>();
        services.AddScoped<Fugue.Application.Features.Modules.IModuleRegistry, ModuleRegistry>();
        services.AddScoped<Fugue.Application.Features.Groups.IGroupAdmin, GroupAdmin>();
        services.AddScoped<Fugue.Application.Features.Overrides.IOverrideAdmin, OverrideAdmin>();
        services.AddScoped<Fugue.Application.Features.Users.IUserDirectory, UserDirectory>();
        services.AddScoped<Fugue.Application.Features.Roles.IRoleCatalog, RoleCatalog>();
        services.AddScoped<IGrantAuditWriter, GrantAuditWriter>();
        services.AddScoped<IIdentitySeeder, IdentitySeeder>();
        return services;
    }
}
