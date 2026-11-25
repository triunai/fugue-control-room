using Fugue.Api.Controllers;
using Fugue.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Tests.Api;

/// <summary>Test-only controller that exercises every <see cref="ApiControllerBase"/> helper.</summary>
public sealed class ProbeController : ApiControllerBase
{
    /// <summary>Exposes the protected status mapping to <c>MapStatusTests</c>.</summary>
    public static int ExposeMapStatus(ErrorType type) => MapStatus(type);

    [HttpGet("things/{id:int}")]
    public Task<ActionResult<Thing>> GetById(int id, CancellationToken ct) => Send(new GetThing(id), ct);

    [HttpPost("things")]
    public Task<ActionResult<Thing>> Create([FromQuery] string name, CancellationToken ct) =>
        SendCreated(new CreateThing(name), nameof(GetById), t => new { id = t.Id }, ct);

    [HttpDelete("things/{id:int}")]
    public Task<IActionResult> Remove(int id, CancellationToken ct) => SendNoContent(new DeleteThing(id), ct);

    [HttpGet("things")]
    public Task<ActionResult<IReadOnlyList<string>>> List([FromQuery] int? limit, CancellationToken ct) =>
        SendPaged(new ListThings(limit), ct);

    [HttpPut("echo")]
    public Task<ActionResult<Echo>> EchoRequestHeaders(CancellationToken ct) =>
        Send(new EchoHeaders(IfMatchVersion, IdempotencyKey), ct);

    [HttpGet("fail")]
    public Task<ActionResult<Thing>> Fail([FromQuery] string code, CancellationToken ct) =>
        Send(new FailWith(FugueErrors.NotFound(code, "Thing was not found.")), ct);

    [HttpGet("invalid")]
    public Task<ActionResult<Thing>> Invalid(CancellationToken ct) =>
        Send(
            new FailWith(FugueErrors.Validation(
                "order.invalid",
                "Order is invalid.",
                new Dictionary<string, string[]> { ["quantity"] = ["Must be positive."] })),
            ct);
}
