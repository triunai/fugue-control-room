using System.Diagnostics;
using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Domain.Results;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fugue.Application.Common.Behaviors;

/// <summary>
/// Wraps every mutating command (ADR-010). It opens the ambient <see cref="IAuditScope"/> (action, actor display, command name) that
/// the SaveChanges interceptor reads, so SUCCESSES are written by the interceptor in the same transaction as the change and this
/// behavior never writes one (no double rows). When the command FAILS (a business error or an unhandled exception) nothing was
/// changed, so it writes ONE <c>outcome = failure</c> row with the error code in its own short transaction. A failing audit write is
/// logged and never changes the command's result. The failure row also carries the error type, the handler's message (never an exception's) and the duration. Queries pass through; a command marked <see cref="NoAuditAttribute"/> is skipped
/// (authentication commands are recorded in <c>access_audit</c>).
/// Order: Logging, Audit, Validation, so validation failures are audited too.
/// </summary>
public sealed partial class AuditBehavior<TMessage, TResponse>(IServiceProvider services)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
    where TResponse : ResultBase, new()
{
    private static readonly bool Applies =
        typeof(IBaseCommand).IsAssignableFrom(typeof(TMessage)) && !Attribute.IsDefined(typeof(TMessage), typeof(NoAuditAttribute));

    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (!Applies)
        {
            return await next(message, cancellationToken);
        }

        // Resolved only for commands: queries and exempt commands never touch the audit services (or the database behind them).
        var scope = services.GetRequiredService<IAuditScope>();
        var trail = services.GetRequiredService<IAuditTrail>();
        var logger = services.GetRequiredService<ILogger<AuditBehavior<TMessage, TResponse>>>();
        string? display = await TryDisplayNameAsync(logger, cancellationToken);
        scope.Begin(
            AuditNaming<TMessage>.Action,
            display,
            new Dictionary<string, object?> { ["command"] = typeof(TMessage).Name });
        long started = Stopwatch.GetTimestamp();
        try
        {
            TResponse response = await next(message, cancellationToken);
            if (response.IsFailed)
            {
                FailureDetail detail = response.Errors is [FugueError fugue, ..]
                    ? new(fugue.Code, fugue.Type.ToString(), Clip(fugue.Message), ElapsedMs(started))
                    : new("unexpected", nameof(ErrorType.Failure), null, ElapsedMs(started));
                await TryRecordFailureAsync(trail, scope, logger, message, detail, cancellationToken);
            }

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The type name only: Exception.Message can carry SQL values or personal data and must never reach an audit row.
            await TryRecordFailureAsync(
                trail, scope, logger, message, new("unhandled", UnhandledType, ex.GetType().FullName, ElapsedMs(started)), cancellationToken);
            throw;
        }
        finally
        {
            scope.End();
        }
    }

    private async Task<string?> TryDisplayNameAsync(ILogger logger, CancellationToken ct)
    {
        if (services.GetRequiredService<ICurrentUser>().UserId is not { } id)
        {
            return null;
        }

        try
        {
            return await services.GetRequiredService<IActorDirectory>().GetDisplayNameAsync(id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDisplayFailed(logger, ex);
            return null;
        }
    }

    private const string UnhandledType = "Unhandled";
    private const int MaxMessageLength = 2000;

    private readonly record struct FailureDetail(string Code, string Type, string? Message, int DurationMs);

    private static string? Clip(string? value) => value is { Length: > MaxMessageLength } ? value[..MaxMessageLength] : value;

    private static int ElapsedMs(long started) => (int)Math.Min(int.MaxValue, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    private static async Task TryRecordFailureAsync(
        IAuditTrail trail, IAuditScope scope, ILogger logger, TMessage message, FailureDetail detail, CancellationToken ct)
    {
        try
        {
            await trail.RecordFailureAsync(
                new AuditFailure(
                    AuditNaming<TMessage>.Action, AuditNaming<TMessage>.EntityType, AuditNaming<TMessage>.EntityId(message), detail.Code, scope.Metadata,
                    detail.Type, detail.Message, detail.DurationMs),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRecordFailed(logger, typeof(TMessage).Name, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Could not resolve the actor display name for an audit row")]
    private static partial void LogDisplayFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Could not write the failure audit row for {MessageName}")]
    private static partial void LogRecordFailed(ILogger logger, string messageName, Exception exception);
}
