using Fugue.Api.OpenApi;
using Fugue.Application.Features.Diagnostics.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>The one real endpoint of the scaffold: proves the pipeline end to end and gives the runbooks something to call.</summary>
public sealed class DiagnosticsController : ApiControllerBase
{
    /// <summary>Round-trips the whole pipeline without a database; `count` must be between 0 and 100.</summary>
    [AllowAnonymous]
    [HttpGet("ping")]
    [ProducesResponseType(typeof(PingResponse), StatusCodes.Status200OK)]
    public Task<ActionResult<PingResponse>> Ping([FromQuery] int count, CancellationToken ct) =>
        Send(new PingQuery(count), ct);
}
