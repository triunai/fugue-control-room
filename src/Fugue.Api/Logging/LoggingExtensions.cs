using System.Diagnostics;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Serilog;
using Serilog.Formatting.Compact;

namespace Fugue.Api.Logging;

public static class LoggingExtensions
{
    /// <summary>
    /// Serilog configured from <c>appsettings</c> (levels, overrides). Development writes readable console
    /// lines; every other environment writes compact JSON, one event per line.
    /// </summary>
    public static IHostBuilder UseFugueSerilog(this IHostBuilder host) =>
        host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext();

            if (context.HostingEnvironment.IsDevelopment())
            {
                configuration.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SagaId} {Message:lj}{NewLine}{Exception}",
                    formatProvider: System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                configuration.WriteTo.Console(new CompactJsonFormatter());
            }
        });

    /// <summary>One structured line per request, enriched with correlation, saga, trace and user ids.</summary>
    public static IApplicationBuilder UseFugueRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
            options.EnrichDiagnosticContext = (diagnostics, http) =>
            {
                ICorrelationContext? correlation = http.RequestServices.GetService<ICorrelationContext>();
                diagnostics.Set("CorrelationId", correlation?.CorrelationId);
                if (correlation?.SagaId is { } saga)
                {
                    diagnostics.Set("SagaId", saga);
                }

                diagnostics.Set("TraceId", Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier);
                if (http.RequestServices.GetService<ICurrentUser>()?.UserId is { } userId)
                {
                    diagnostics.Set("UserId", userId);
                }
            });
}
