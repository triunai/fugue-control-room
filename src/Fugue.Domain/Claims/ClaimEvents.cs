using Fugue.Domain.Events;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Claims;

/// <summary>
/// The claim's outbox events (ADR-011, D-053). Payloads are deliberately thin: the claim id, its number, the resulting status and the market. Never the
/// description, the reserve or an amount: a consumer that needs more reads the claim over REST. Names carry no underscore (the event-name fitness
/// function allows <c>[a-z0-9]</c> segments), so "info requested" is <c>claim.info.requested.v1</c>.
/// </summary>
public sealed record ClaimSubmitted(ClaimId Id, string Number, ClaimStatus Status, Market Market, LineOfBusiness Line) : IDomainEvent
{
    public const string Type = "claim.submitted.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => ClaimEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

public sealed record ClaimAssigned(ClaimId Id, string Number, ClaimStatus Status, Market Market) : IDomainEvent
{
    public const string Type = "claim.assigned.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => ClaimEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

public sealed record ClaimInfoRequested(ClaimId Id, string Number, ClaimStatus Status, Market Market) : IDomainEvent
{
    public const string Type = "claim.info.requested.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => ClaimEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

public sealed record ClaimInfoProvided(ClaimId Id, string Number, ClaimStatus Status, Market Market) : IDomainEvent
{
    public const string Type = "claim.info.provided.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => ClaimEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

/// <summary>Approved or rejected: <c>Status</c> says which.</summary>
public sealed record ClaimDecided(ClaimId Id, string Number, ClaimStatus Status, Market Market) : IDomainEvent
{
    public const string Type = "claim.decided.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => ClaimEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

public sealed record ClaimSettled(ClaimId Id, string Number, ClaimStatus Status, Market Market) : IDomainEvent
{
    public const string Type = "claim.settled.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => ClaimEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

/// <summary>The internal reserve was set or revised. Thin like the others: no amount (the reserve is internal; a consumer reads the claim over REST).</summary>
public sealed record ClaimReserveSet(ClaimId Id, string Number, ClaimStatus Status, Market Market) : IDomainEvent
{
    public const string Type = "claim.reserve.set.v1";

    string IDomainEvent.EventType => Type;

    string IDomainEvent.AggregateType => ClaimEventNames.Aggregate;

    string IDomainEvent.AggregateId => Id.ToString();
}

internal static class ClaimEventNames
{
    public const string Aggregate = "claim";
}
