using Fugue.Api.Auditing;
using Fugue.Api.Auth;
using Fugue.Api.Correlation;
using Fugue.Api.Cors;
using Fugue.Api.Http;
using Fugue.Api.Logging;
using Fugue.Api.OpenApi;
using Fugue.Api.Seeding;
using Fugue.Application;
using Fugue.Application.Abstractions;
using Fugue.Application.Common.Behaviors;
using Fugue.Domain.Abstractions;
using Fugue.Infrastructure;
using Fugue.Infrastructure.Audit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Serilog;

// Bootstrap logger: covers startup failures before the configured logger exists.
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture).CreateLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseFugueSerilog();

    builder.Services.AddRouting(options => options.LowercaseUrls = true);
    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
            // Framework-generated 400s (model binding, invalid model state) go through the same ProblemResult
            // and enrichment as handler failures: application/problem+json with code, traceId, correlationId.
            options.InvalidModelStateResponseFactory = context =>
            {
                var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                ValidationProblemDetails problem = factory.CreateValidationProblemDetails(
                    context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest);
                problem.Extensions["code"] = "validation.failed";
                ProblemDetailsEnrichment.AddCorrelation(problem, context.HttpContext);
                return new ProblemResult(problem);
            });
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
            ProblemDetailsEnrichment.AddCorrelation(context.ProblemDetails, context.HttpContext));
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddFugueOpenApi();
    builder.Services.AddConsoleCors(builder.Configuration, builder.Environment);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, JwtCurrentUser>();

    // Authentication (JWT HS256) and authorization (effective permissions). The fallback policy makes every endpoint
    // require an authenticated caller; anonymous endpoints are explicit [AllowAnonymous] exceptions.
    builder.Services.AddOptions<AuthOptions>().Bind(builder.Configuration.GetSection(AuthOptions.Section)).ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
    builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerSetup>();
    builder.Services.AddSingleton<UnauthenticatedAuditLimiter>();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
    builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
    builder.Services.AddAuthRateLimiting(builder.Configuration);
    builder.Services.AddScoped<CorrelationContext>();
    builder.Services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());
    builder.Services.AddOptions<AuditOptions>().Bind(builder.Configuration.GetSection(AuditOptions.Section)).ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<AuditOptions>, AuditOptionsValidator>();
    builder.Services.AddScoped<IAuditRequestInfo, HttpAuditRequestInfo>(); // before AddInfrastructure, which only adds a null default
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Development-only seeding: opt in with `dotnet run -- --seed` (or Seed:Enabled=true). Never in tests or other environments.
    if (builder.Environment.IsDevelopment() && (args.Contains("--seed") || builder.Configuration.GetValue<bool>("Seed:Enabled")))
    {
        builder.Services.AddHostedService<DevelopmentSeedHostedService>();
    }
    builder.Services.AddMediator(options =>
    {
        options.ServiceLifetime = ServiceLifetime.Scoped;

        // Outermost first: logging sees everything; audit wraps validation so validation failures get a failure row (ADR-010).
        options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(AuditBehavior<,>), typeof(ValidationBehavior<,>)];
    });

    WebApplication app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseFugueRequestLogging();
    app.UseExceptionHandler();
    app.UseCors(ConsoleCors.PolicyName); // before authentication: a preflight carries no token and must be answered here
    app.UseFugueOpenApi(); // Development only
    app.UseStatusCodePages(); // empty 404/405/415 etc. become problem+json via AddProblemDetails
    app.UseAuthentication();
    app.UseMiddleware<Fugue.Api.Modules.ModuleGateMiddleware>();
    app.UseRateLimiter();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposes the entry point to <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
