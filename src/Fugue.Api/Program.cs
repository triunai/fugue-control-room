using Fugue.Api.Auth;
using Fugue.Api.Correlation;
using Fugue.Api.Http;
using Fugue.Api.Logging;
using Fugue.Application;
using Fugue.Application.Abstractions;
using Fugue.Application.Common.Behaviors;
using Fugue.Domain.Abstractions;
using Serilog;

// Bootstrap logger: covers startup failures before the configured logger exists.
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture).CreateLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseFugueSerilog();

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
            ProblemDetailsEnrichment.AddCorrelation(context.ProblemDetails, context.HttpContext));
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
    builder.Services.AddScoped<CorrelationContext>();
    builder.Services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());
    builder.Services.AddApplication();
    builder.Services.AddMediator(options =>
    {
        options.ServiceLifetime = ServiceLifetime.Scoped;

        // Outermost first: logging sees validation failures.
        options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
    });

    WebApplication app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseFugueRequestLogging();
    app.UseExceptionHandler();
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
