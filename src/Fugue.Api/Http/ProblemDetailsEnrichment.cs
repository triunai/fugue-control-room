using System.Diagnostics;
using Fugue.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Http;

/// <summary>Adds the ids every problem response carries: <c>traceId</c> and <c>correlationId</c>.</summary>
internal static class ProblemDetailsEnrichment
{
    public static string TraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    public static void AddCorrelation(ProblemDetails problem, HttpContext context)
    {
        problem.Extensions["traceId"] = TraceId(context);
        string? correlationId = context.RequestServices.GetService<ICorrelationContext>()?.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }
    }
}
