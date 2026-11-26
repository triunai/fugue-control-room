namespace Fugue.Api.Contracts;

/// <summary>Body of a customer create. <c>UserId</c> (the owner) is honoured only for callers with effective CUSTOMERS_ALL.</summary>
public sealed record CreateCustomerRequest(string Name, string Email, string? Phone, Guid? UserId = null);

/// <summary>Body of a customer replace; the route carries the id and <c>If-Match</c> the version.</summary>
public sealed record UpdateCustomerRequest(string Name, string Email, string? Phone);
