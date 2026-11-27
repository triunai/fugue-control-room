using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Modules.Commands;
using Fugue.Application.Features.Modules.Models;
using Fugue.Application.Features.Modules.Queries;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Module management (ADR-009): see the configuration, database and effective state, and flip the runtime switch.</summary>
public sealed class ModulesController : ApiControllerBase
{
    /// <summary>List modules with the configuration switch, the database switch and the resulting effective state.</summary>
    [HttpGet]
    [HasPermission(Permissions.ModulesView)]
    [ProducesResponseType(typeof(IReadOnlyList<ModuleDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<ModuleDto>>> List(CancellationToken ct) =>
        Send(new ListModulesQuery(), ct);

    /// <summary>Switch a module on or off at runtime. The SYSTEM module cannot be disabled (409 `module.system_protected`).</summary>
    [HttpPatch("{code}")]
    [HasPermission(Permissions.ModulesManage)]
    [IfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "module.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "module.system_protected")]
    public Task<IActionResult> SetActive(string code, SetModuleActiveRequest body, CancellationToken ct) =>
        SendNoContent(new SetModuleActiveCommand(code, body.IsActive, IfMatchVersion), ct);
}
