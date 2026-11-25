using System.Diagnostics;
using FluentResults;
using Fugue.Domain.Results;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Fugue.Application.Common.Behaviors;

/// <summary>
/// Logs the message type name, elapsed milliseconds and outcome. Failures log their error codes at
/// Warning. The message payload is never logged (it may hold personal data).
/// </summary>
public sealed partial class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
    where TResponse : ResultBase, new()
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        string name = typeof(TMessage).Name;
        long start = Stopwatch.GetTimestamp();

        TResponse response = await next(message, cancellationToken);

        double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (response.IsSuccess)
        {
            LogSuccess(logger, name, elapsedMs);
        }
        else
        {
            string codes = string.Join(",", response.Errors.Select(e => e is FugueError f ? f.Code : "unexpected"));
            LogFailure(logger, name, elapsedMs, codes);
        }

        return response;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Handled {MessageName} in {ElapsedMs:0.0} ms: success")]
    private static partial void LogSuccess(ILogger logger, string messageName, double elapsedMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Handled {MessageName} in {ElapsedMs:0.0} ms: failure {ErrorCodes}")]
    private static partial void LogFailure(ILogger logger, string messageName, double elapsedMs, string errorCodes);
}
