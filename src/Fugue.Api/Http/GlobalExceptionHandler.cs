using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Http;

/// <summary>
/// Turns an unhandled exception into an RFC 9457 500 problem. Exception detail is exposed only in
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

        LogUnhandled(logger, exception);

        var problem = new ProblemDetails
        {
            Type = "https://fugue.dev/errors/unexpected",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
        };
        problem.Extensions["code"] = "unexpected";
        if (environment.IsDevelopment())
        {
            problem.Detail = exception.Message;
            problem.Extensions["exception"] = exception.ToString();
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Request aborted by the client")]
    private static partial void LogClientClosed(ILogger logger);
}
