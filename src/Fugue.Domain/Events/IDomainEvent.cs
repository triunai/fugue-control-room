namespace Fugue.Domain.Events;

/// <summary>
/// Something that happened to an aggregate and that other systems may care about (ADR-011). It becomes an outbox row in the SAME
/// transaction as the change. Every implementation declares <c>public const string Type</c>, a versioned name such as
/// <c>customer.created.v1</c>; a fitness function checks the form and uniqueness. A breaking payload change means a new <c>v2</c> type, never an edit.
/// The event's public properties are its payload: keep it thin (ids and the resulting state), personal data is masked before it is stored.
/// </summary>
public interface IDomainEvent
{
    /// <summary>The versioned name (the implementation's <c>Type</c> constant).</summary>
    string EventType { get; }

    string AggregateType { get; }

    string AggregateId { get; }
}

/// <summary>An aggregate that raises <see cref="IDomainEvent"/>s. The persistence layer drains them when the aggregate is saved.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
