using Fugue.Api.Contracts;
using Fugue.Application.Features.Customers;
using Fugue.Application.Features.Customers.Commands;
using Fugue.Application.Features.Customers.Queries;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Customers: a stateful aggregate rehearsing ETag/If-Match, keyset paging and guarded transitions. One-liners only.</summary>
public sealed class CustomersController : ApiControllerBase
{
    [HttpPost]
    public Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest body, CancellationToken ct) =>
        SendCreated(new CreateCustomerCommand(body.Name, body.Email, body.Phone), nameof(Get), c => new { id = c.Id }, ct);

    [HttpGet("{id}")]
    public Task<ActionResult<CustomerDto>> Get(CustomerId id, CancellationToken ct) =>
        Send(new GetCustomerByIdQuery(id), ct);

    [HttpGet]
    public Task<ActionResult<IReadOnlyList<CustomerDto>>> List(
        [FromQuery] int? limit, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] string? cursor, CancellationToken ct) =>
        SendPaged(new ListCustomersQuery(limit, status, search, cursor), ct);

    [HttpPut("{id}")]
    public Task<ActionResult<CustomerDto>> Update(CustomerId id, UpdateCustomerRequest body, CancellationToken ct) =>
        Send(new UpdateCustomerCommand(id, body.Name, body.Email, body.Phone, IfMatchVersion), ct);

    [HttpPost("{id}/suspend")]
    public Task<ActionResult<CustomerDto>> Suspend(CustomerId id, CancellationToken ct) =>
        Send(new SuspendCustomerCommand(id, IfMatchVersion), ct);

    [HttpPost("{id}/reactivate")]
    public Task<ActionResult<CustomerDto>> Reactivate(CustomerId id, CancellationToken ct) =>
        Send(new ReactivateCustomerCommand(id, IfMatchVersion), ct);

    [HttpDelete("{id}")]
    public Task<IActionResult> Delete(CustomerId id, CancellationToken ct) =>
        SendNoContent(new DeleteCustomerCommand(id, IfMatchVersion), ct);
}
