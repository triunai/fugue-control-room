using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Me.Models;
using Fugue.Application.Features.Me.Queries;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>The signed-in caller's own identity and effective access. Needs no permission: every authenticated user may read their own.</summary>
[Module(ModuleCodes.System)]
public sealed class MeController : ApiControllerBase
{
    /// <summary>The caller's identity, roles, groups, effective permission codes and the enabled modules.</summary>
    /// <remarks>
    /// Drives the console's navigation and permission checks. The permission list is exactly what the API enforces (deny beats allow beats role;
    /// permissions of a disabled module are absent), looked up fresh on every call and never cached by clients or proxies.
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(MeDto), StatusCodes.Status200OK)]
    public Task<ActionResult<MeDto>> Get(CancellationToken ct) =>
        Send(new GetMeQuery(), ct);
}
