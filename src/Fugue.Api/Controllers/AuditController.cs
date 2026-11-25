using Fugue.Api.Auth;
using Fugue.Api.Modules;
using Fugue.Application.Features.Audit;
using Fugue.Application.Features.Audit.Queries;
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
    [HttpGet]
    [HasPermission(Permissions.AuditRead)]
    public Task<ActionResult<IReadOnlyList<AuditLogDto>>> List(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] Guid? actorUserId,
        [FromQuery] string? action,
        [FromQuery] string? correlationId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken ct) =>
        SendPaged(new ListAuditLogQuery(entityType, entityId, actorUserId, action, correlationId, from, to, limit, cursor), ct);
}
