using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Http;

/// <summary>
/// Turns an unhandled exception into an RFC 9457 problem: 503 + Retry-After for a transient database or timeout
/// failure, otherwise 500. Exception detail is exposed only in
/// Development. A cancellation caused by the client aborting the request is logged at Information and answered
/// with 499 (client closed request) and no body.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogClientClosed(logger);
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        bool unavailable = IsTransientDependencyFailure(exception);
        if (unavailable)
        {
            LogDependencyUnavailable(logger, exception);
        }
        else
        {
            LogUnhandled(logger, exception);
        }

        int status = unavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        string code = unavailable ? "dependency.unavailable" : "unexpected";
        var problem = new ProblemDetails
        {
            Type = unavailable ? "https://fugue.dev/errors/dependency-unavailable" : "https://fugue.dev/errors/unexpected",
            Title = unavailable ? "A dependency is temporarily unavailable. Retry shortly." : "An unexpected error occurred.",
            Status = status,
        };
        problem.Extensions["code"] = code;
        if (unavailable)
        {
            httpContext.Response.Headers.RetryAfter = RetryAfterSeconds;
        }
        if (environment.IsDevelopment())
        {
            problem.Detail = exception.Message;
            problem.Extensions["exception"] = exception.ToString();
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private const string RetryAfterSeconds = "5";

    /// <summary>
    /// True when the exception chain contains a transient database failure (<see cref="DbException.IsTransient"/>,
    /// which Npgsql sets for connection loss, failover and similar) or a timeout. Walking the chain matters because
    /// EF Core wraps provider exceptions (DbUpdateException, retry-strategy InvalidOperationException).
    /// Uses the provider-neutral DbException so the API layer never references Npgsql.
    /// </summary>
    internal static bool IsTransientDependencyFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { IsTransient: true } or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning, Message = "Dependency unavailable; answering 503")]
    private static partial void LogDependencyUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 100, Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Request aborted by the client")]
    private static partial void LogClientClosed(ILogger logger);
}
