using Fugue.Application.Features.Diagnostics.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>The one real endpoint of the scaffold: proves the pipeline end to end and gives the runbooks something to call.</summary>
public sealed class DiagnosticsController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("ping")]
    public Task<ActionResult<PingResponse>> Ping([FromQuery] int count, CancellationToken ct) =>
        Send(new PingQuery(count), ct);
}
