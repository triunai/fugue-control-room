using Fugue.Domain.Customers;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Customers;

/// <summary>The customer as returned to clients. <see cref="Version"/> is also sent as the <c>ETag</c>.</summary>
public sealed record CustomerDto(
    CustomerId Id,
    string Name,
    string Email,
    string? Phone,
    CustomerStatus Status,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    long Version,
    UserId? OwnerId = null) : IVersioned;
