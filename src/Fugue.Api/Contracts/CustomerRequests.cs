namespace Fugue.Api.Contracts;

/// <summary>Request bodies. <c>UserId</c> on create is honoured only for callers with effective CUSTOMERS_ALL. Handlers take plain commands; these exist only so the route id and the If-Match header stay out of the body.</summary>
public sealed record CreateCustomerRequest(string Name, string Email, string? Phone, Guid? UserId = null);

public sealed record UpdateCustomerRequest(string Name, string Email, string? Phone);
