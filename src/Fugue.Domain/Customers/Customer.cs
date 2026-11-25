using FluentResults;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Domain.Customers;

/// <summary>
/// A stateful aggregate used to rehearse the full persistence story: audit fields, optimistic concurrency
/// (<see cref="IVersioned"/> backed by Postgres <c>xmin</c>) and guarded state transitions.
/// </summary>
public sealed class Customer : IAuditable, IVersioned
{
    private Customer()
    {
        Name = string.Empty;
        Email = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
    }

    public CustomerId Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Unique, stored lower-cased.</summary>
    public string Email { get; private set; }

    /// <summary>Optional, E.164 (validated at the application boundary).</summary>
    public string? Phone { get; private set; }

    public CustomerStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; }

    /// <summary>Postgres <c>xmin</c> system column; the optimistic-concurrency token.</summary>
    public uint RowVersion { get; private set; }

    long IVersioned.Version => RowVersion;

    public static Customer Create(string name, string email, string? phone) =>
        new()
        {
            Id = CustomerId.New(),
            Name = Normalize(name),
            Email = NormalizeEmail(email),
            Phone = NormalizePhone(phone),
            Status = CustomerStatus.Active,
        };

    public void Update(string name, string email, string? phone)
    {
        Name = Normalize(name);
        Email = NormalizeEmail(email);
        Phone = NormalizePhone(phone);
    }

    public Result Suspend() =>
        Status == CustomerStatus.Active
            ? Transition(CustomerStatus.Suspended)
            : Result.Fail(InvalidTransition("suspend"));

    public Result Reactivate() =>
        Status == CustomerStatus.Suspended
            ? Transition(CustomerStatus.Active)
            : Result.Fail(InvalidTransition("reactivate"));

    private Result Transition(CustomerStatus next)
    {
        Status = next;
        return Result.Ok();
    }

    private FugueError InvalidTransition(string verb) =>
        FugueErrors.Conflict("customer.invalid_transition", $"A customer that is {Status} cannot be {verb}d.");

    private static string Normalize(string value) => value.Trim();

    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();

    private static string? NormalizePhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
