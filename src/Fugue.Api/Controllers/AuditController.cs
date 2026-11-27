using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Audit;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>
/// The audit log (ADR-010): read-only, newest first, keyset paged. There is no write, update or delete endpoint, and the table
/// refuses them anyway (append-only trigger). One-liners only.
/// </summary>
[Module(ModuleCodes.Audit)]
public sealed class AuditController : ApiControllerBase
{
    /// <summary>List audit rows, newest first, keyset paged. Every filter is optional and they combine.</summary>
    [HttpGet]
    [HasPermission(Permissions.AuditRead)]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<AuditLogDto>>> List([FromQuery] AuditListFilter filter, CancellationToken ct) =>
        SendPaged(filter.ToQuery(), ct);
}
