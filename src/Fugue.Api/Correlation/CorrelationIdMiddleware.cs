using System.Diagnostics;
using Fugue.Application.Abstractions;
using Serilog.Context;

namespace Fugue.Api.Correlation;

/// <summary>
/// First middleware in the pipeline (ADR-005). Accepts an inbound <c>X-Correlation-Id</c> only if it is a valid
/// id, otherwise generates one; accepts <c>X-Saga-Id</c> only if valid. Echoes both on the response, and
/// publishes them to the Serilog <c>LogContext</c> and the current <see cref="Activity"/>.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CorrelationContext correlation)
    {
        string? inboundCorrelation = context.Request.Headers[CorrelationHeaders.CorrelationId].FirstOrDefault();
        string correlationId = CorrelationIdFormat.IsValid(inboundCorrelation)
            ? inboundCorrelation!
            : Guid.NewGuid().ToString("N");

        string? inboundSaga = context.Request.Headers[CorrelationHeaders.SagaId].FirstOrDefault();
        string? sagaId = CorrelationIdFormat.IsValid(inboundSaga) ? inboundSaga : null;

        correlation.Initialize(correlationId, sagaId);

        // Hosting only creates an Activity when something listens. Ensure one so a trace id always exists.
        Activity? owned = null;
        if (Activity.Current is null)
        {
            owned = new Activity("Fugue.Request");
            owned.Start();
        }

        Activity? activity = Activity.Current;
        activity?.SetTag("correlation.id", correlationId);
        activity?.AddBaggage("correlation.id", correlationId);

        // Set at response start so the headers survive the exception handler clearing the response.
        context.Response.OnStarting(
            static state =>
            {
                (HttpContext http, CorrelationContext c) = ((HttpContext, CorrelationContext))state;
                http.Response.Headers[CorrelationHeaders.CorrelationId] = c.CorrelationId;
                if (c.SagaId is not null)
                {
                    http.Response.Headers[CorrelationHeaders.SagaId] = c.SagaId;
                }

                return Task.CompletedTask;
            },
            (context, correlation));

        try
        {
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                if (sagaId is not null)
                {
                    CorrelationContext.Publish(sagaId);
                }

                await next(context);
            }
        }
        finally
        {
            owned?.Stop();
        }
    }
}
