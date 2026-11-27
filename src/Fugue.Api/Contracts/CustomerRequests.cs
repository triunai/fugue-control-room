using Fugue.Application.Features.Customers.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Contracts;

/// <summary>Body of a customer create. <c>UserId</c> (the owner) is honoured only for callers with effective CUSTOMERS_ALL.</summary>
public sealed record CreateCustomerRequest(string Name, string Email, string? Phone, Guid? UserId = null);

/// <summary>Body of a customer replace; the route carries the id and <c>If-Match</c> the version.</summary>
public sealed record UpdateCustomerRequest(string Name, string Email, string? Phone);

/// <summary>Query parameters of the customers list, bound as one object. <c>Status</c> is Active or Suspended; <c>Search</c> is a case-insensitive match over name and email.</summary>
public sealed record CustomerListFilter
{
    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    [FromQuery(Name = "status")]
    public string? Status { get; init; }

    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    public ListCustomersQuery ToQuery() => new(Limit, Status, Search, Cursor);
}
