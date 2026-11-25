using System.Diagnostics;
using Fugue.Application.Abstractions;
using Serilog.Context;

namespace Fugue.Api.Correlation;

/// <summary>Scoped holder of the request's correlation and saga ids. Initialised by <see cref="CorrelationIdMiddleware"/>.</summary>
public sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; private set; } = string.Empty;

    public string? SagaId { get; private set; }

    internal void Initialize(string correlationId, string? sagaId)
    {
        CorrelationId = correlationId;
        SagaId = sagaId;
    }

    public void SetSaga(string sagaId)
    {
        if (!CorrelationIdFormat.IsValid(sagaId))
        {
            throw new ArgumentException(
                $"A saga id must be 1 to {CorrelationIdFormat.MaxLength} characters of [A-Za-z0-9-_].", nameof(sagaId));
        }

        SagaId = sagaId;
        Publish(sagaId);
    }

    /// <summary>Pushes the saga id into the log context and the current activity (tag and baggage).</summary>
    internal static void Publish(string sagaId)
    {
        // The pushed property lives in the request's async context; it is released when that context ends.
        _ = LogContext.PushProperty("SagaId", sagaId);
        Activity? activity = Activity.Current;
        activity?.SetTag("saga.id", sagaId);
        activity?.AddBaggage("saga.id", sagaId);
    }
}
