using Fugue.Domain.Events;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Customers;

/// <summary>Payload properties are what leaves the system; <c>Email</c> and <c>Phone</c> are partially masked on the way out (ADR-011).</summary>
public sealed record CustomerCreated(CustomerId Id, string Name, string Email, string? Phone, CustomerStatus Status, UserId? OwnerUserId) : IDomainEvent
{
    public const string Type = "customer.created.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => CustomerEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

public sealed record CustomerUpdated(CustomerId Id, string Name, string Email, string? Phone) : IDomainEvent
{
    public const string Type = "customer.updated.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => CustomerEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

public sealed record CustomerSuspended(CustomerId Id, CustomerStatus Status) : IDomainEvent
{
    public const string Type = "customer.suspended.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => CustomerEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

public sealed record CustomerReactivated(CustomerId Id, CustomerStatus Status) : IDomainEvent
{
    public const string Type = "customer.reactivated.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => CustomerEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

public sealed record CustomerDeleted(CustomerId Id) : IDomainEvent
{
    public const string Type = "customer.deleted.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => CustomerEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

internal static class CustomerEventNames
{
    public const string Aggregate = "customer";
}
