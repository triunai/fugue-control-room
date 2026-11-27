using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Idempotency;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Customers;
using Fugue.Application.Features.Customers.Commands;
using Fugue.Application.Features.Customers.Queries;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Customers: a stateful aggregate rehearsing ETag/If-Match, keyset paging and guarded transitions. One-liners only.</summary>
[Module(ModuleCodes.Customers)]
public sealed class CustomersController : ApiControllerBase
{
    /// <summary>Create a customer. A `userId` in the body (the owner) is honoured only for callers with CUSTOMERS_ALL; otherwise the caller owns it.</summary>
    [HttpPost]
    [HasPermission(Permissions.CustomersInsert)]
    [Idempotent]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status409Conflict, "customer.email_taken")]
    public Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest body, CancellationToken ct) =>
        SendCreated(new CreateCustomerCommand(body.Name, body.Email, body.Phone, body.UserId), nameof(Get), c => new { id = c.Id }, ct);

    /// <summary>Get one customer. A self-service caller sees only their own; anything else is 404, never 403.</summary>
    [HttpGet("{id}")]
    [HasPermission(Permissions.CustomersView)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "customer.not_found")]
    public Task<ActionResult<CustomerDto>> Get(CustomerId id, CancellationToken ct) =>
        Send(new GetCustomerByIdQuery(id), ct);

    /// <summary>List customers, newest first, keyset paged. Filter by `status` (Active or Suspended) and a case-insensitive `search` over name and email.</summary>
    [HttpGet]
    [HasPermission(Permissions.CustomersView)]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<CustomerDto>>> List(
        [FromQuery] int? limit, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] string? cursor, CancellationToken ct) =>
        SendPaged(new ListCustomersQuery(limit, status, search, cursor), ct);

    /// <summary>Replace a customer's name, email and phone. Changing the email needs staff access (403 `customer.email_change_forbidden` otherwise).</summary>
    [HttpPut("{id}")]
    [HasPermission(Permissions.CustomersEdit)]
    [IfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "customer.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "customer.email_taken")]
    public Task<IActionResult> Update(CustomerId id, UpdateCustomerRequest body, CancellationToken ct) =>
        SendNoContent(new UpdateCustomerCommand(id, body.Name, body.Email, body.Phone, IfMatchVersion), ct);

    /// <summary>Suspend an active customer.</summary>
    [HttpPost("{id}/suspend")]
    [HasPermission(Permissions.CustomersSuspend)]
    [IfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "customer.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "customer.invalid_transition")]
    public Task<IActionResult> Suspend(CustomerId id, CancellationToken ct) =>
        SendNoContent(new SuspendCustomerCommand(id, IfMatchVersion), ct);

    /// <summary>Reactivate a suspended customer.</summary>
    [HttpPost("{id}/reactivate")]
    [HasPermission(Permissions.CustomersSuspend)]
    [IfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "customer.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "customer.invalid_transition")]
    public Task<IActionResult> Reactivate(CustomerId id, CancellationToken ct) =>
        SendNoContent(new ReactivateCustomerCommand(id, IfMatchVersion), ct);

    /// <summary>Soft-delete a customer; it then answers 404.</summary>
    [HttpDelete("{id}")]
    [HasPermission(Permissions.CustomersDelete)]
    [IfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "customer.not_found")]
    public Task<IActionResult> Delete(CustomerId id, CancellationToken ct) =>
        SendNoContent(new DeleteCustomerCommand(id, IfMatchVersion), ct);
}
