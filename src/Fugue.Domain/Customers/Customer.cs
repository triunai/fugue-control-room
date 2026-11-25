using FluentResults;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Events;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Domain.Customers;

/// <summary>
/// A stateful aggregate used to rehearse the full persistence story: audit fields, optimistic concurrency
/// (<see cref="IVersioned"/> backed by Postgres <c>xmin</c>) and guarded state transitions.
/// </summary>
public sealed class Customer : IAuditable, IVersioned, IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    private Customer()
    {
        Name = string.Empty;
        Email = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
    }

    public CustomerId Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Stored lower-cased; unique per owner (D-034).</summary>
    public string Email { get; private set; }

    /// <summary>Optional, E.164 (validated at the application boundary).</summary>
    public string? Phone { get; private set; }

    public CustomerStatus Status { get; private set; }

    /// <summary>The owning user; null for an unowned record. Visible only to its owner or to <c>CUSTOMERS_ALL</c> holders.</summary>
    public UserId? UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; }

    /// <summary>Postgres <c>xmin</c> system column; the optimistic-concurrency token.</summary>
    public uint RowVersion { get; private set; }

    long IVersioned.Version => RowVersion;

    /// <summary>Events raised since the aggregate was loaded or created; the persistence layer turns them into outbox rows (ADR-011). Explicit, so EF does not map it.</summary>
    IReadOnlyCollection<IDomainEvent> IHasDomainEvents.DomainEvents => _events;

    void IHasDomainEvents.ClearDomainEvents() => _events.Clear();

    public static Customer Create(string name, string email, string? phone, UserId? owner = null)
    {
        var customer = new Customer
        {
            Id = CustomerId.New(),
            Name = Normalize(name),
            Email = NormalizeEmail(email),
            Phone = NormalizePhone(phone),
            Status = CustomerStatus.Active,
            UserId = owner,
        };
        customer._events.Add(new CustomerCreated(customer.Id, customer.Name, customer.Email, customer.Phone, customer.Status, customer.UserId));
        return customer;
    }

    /// <summary>Raises <see cref="CustomerUpdated"/> only when something actually changed (a no-op update is not an event).</summary>
    public void Update(string name, string email, string? phone)
    {
        string newName = Normalize(name);
        string newEmail = NormalizeEmail(email);
        string? newPhone = NormalizePhone(phone);
        bool changed = newName != Name || newEmail != Email || newPhone != Phone;

        Name = newName;
        Email = newEmail;
        Phone = newPhone;
        if (changed)
        {
            _events.Add(new CustomerUpdated(Id, Name, Email, Phone));
        }
    }

    public Result Suspend()
    {
        if (Status != CustomerStatus.Active)
        {
            return Result.Fail(InvalidTransition("suspend"));
        }

        Status = CustomerStatus.Suspended;
        _events.Add(new CustomerSuspended(Id, Status));
        return Result.Ok();
    }

    public Result Reactivate()
    {
        if (Status != CustomerStatus.Suspended)
        {
            return Result.Fail(InvalidTransition("reactivate"));
        }

        Status = CustomerStatus.Active;
        _events.Add(new CustomerReactivated(Id, Status));
        return Result.Ok();
    }

    /// <summary>Records that the aggregate is being removed; call it before the repository deletes the row, so the event commits with the delete.</summary>
    public void MarkDeleted() => _events.Add(new CustomerDeleted(Id));

    private FugueError InvalidTransition(string verb) =>
        FugueErrors.Conflict("customer.invalid_transition", $"A customer that is {Status} cannot be {verb}d.");

    private static string Normalize(string value) => value.Trim();

    private static string NormalizeEmail(string value) => Identity.EmailKey.Canonicalize(value);

    private static string? NormalizePhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
