namespace Fugue.Api.Contracts;

/// <summary>Request bodies. Handlers take plain commands; these exist only so the route id and the If-Match header stay out of the body.</summary>
public sealed record CreateCustomerRequest(string Name, string Email, string? Phone);

public sealed record UpdateCustomerRequest(string Name, string Email, string? Phone);
