using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Application.Features.Modules;
using Fugue.Application.Features.Modules.Commands;
using Fugue.Application.Features.Modules.Queries;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>Module management (ADR-009): see the configuration, database and effective state, and flip the runtime switch.</summary>
public sealed class ModulesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ModulesView)]
    public Task<ActionResult<IReadOnlyList<ModuleDto>>> List(CancellationToken ct) =>
        Send(new ListModulesQuery(), ct);

    [HttpPatch("{code}")]
    [HasPermission(Permissions.ModulesManage)]
    public Task<IActionResult> SetActive(string code, SetModuleActiveRequest body, CancellationToken ct) =>
        SendNoContent(new SetModuleActiveCommand(code, body.IsActive, IfMatchVersion), ct);
}
