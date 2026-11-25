using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
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
    [HttpPost]
    [HasPermission(Permissions.CustomersInsert)]
    public Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest body, CancellationToken ct) =>
        SendCreated(new CreateCustomerCommand(body.Name, body.Email, body.Phone, body.UserId), nameof(Get), c => new { id = c.Id }, ct);

    [HttpGet("{id}")]
    [HasPermission(Permissions.CustomersView)]
    public Task<ActionResult<CustomerDto>> Get(CustomerId id, CancellationToken ct) =>
        Send(new GetCustomerByIdQuery(id), ct);

    [HttpGet]
    [HasPermission(Permissions.CustomersView)]
    public Task<ActionResult<IReadOnlyList<CustomerDto>>> List(
        [FromQuery] int? limit, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] string? cursor, CancellationToken ct) =>
        SendPaged(new ListCustomersQuery(limit, status, search, cursor), ct);

    [HttpPut("{id}")]
    [HasPermission(Permissions.CustomersEdit)]
    public Task<IActionResult> Update(CustomerId id, UpdateCustomerRequest body, CancellationToken ct) =>
        SendNoContent(new UpdateCustomerCommand(id, body.Name, body.Email, body.Phone, IfMatchVersion), ct);

    [HttpPost("{id}/suspend")]
    [HasPermission(Permissions.CustomersSuspend)]
    public Task<IActionResult> Suspend(CustomerId id, CancellationToken ct) =>
        SendNoContent(new SuspendCustomerCommand(id, IfMatchVersion), ct);

    [HttpPost("{id}/reactivate")]
    [HasPermission(Permissions.CustomersSuspend)]
    public Task<IActionResult> Reactivate(CustomerId id, CancellationToken ct) =>
        SendNoContent(new ReactivateCustomerCommand(id, IfMatchVersion), ct);

    [HttpDelete("{id}")]
    [HasPermission(Permissions.CustomersDelete)]
    public Task<IActionResult> Delete(CustomerId id, CancellationToken ct) =>
        SendNoContent(new DeleteCustomerCommand(id, IfMatchVersion), ct);
}
