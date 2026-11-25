namespace Fugue.Application.Abstractions;

/// <summary>
/// Per-request correlation identifiers (ADR-005). <see cref="CorrelationId"/> identifies the request chain;
/// <see cref="SagaId"/> identifies a long-running business process (for example the aggregate id of a
/// workflow) and may be supplied by the caller or set by a handler.
/// </summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }

    string? SagaId { get; }

    /// <summary>Sets the saga id. Throws <see cref="ArgumentException"/> when it is not a valid id (<see cref="CorrelationIdFormat"/>).</summary>
    void SetSaga(string sagaId);
}
