using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
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
    /// <summary>List audit rows, newest first, keyset paged. Every filter is optional and they combine.</summary>
    [HttpGet]
    [HasPermission(Permissions.AuditRead)]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<AuditLogDto>>> List([FromQuery] AuditListFilter filter, CancellationToken ct) =>
        SendPaged(filter.ToQuery(), ct);

    /// <summary>
    /// Full-text search over the audit trail (D-058): typo tolerant, newest first, with highlight snippets. Eventually consistent with the log
    /// (an indexer copies rows). 503 <c>search.unavailable</c> when the search engine is not configured or unreachable.
    /// </summary>
    [HttpGet("search")]
    [HasPermission(Permissions.AuditRead)]
    [ProducesResponseType(typeof(AuditSearchResultDto), StatusCodes.Status200OK)]
    public Task<ActionResult<AuditSearchResultDto>> Search([FromQuery] AuditSearchRequest filter, CancellationToken ct) =>
        Send(filter.ToQuery(), ct);

    /// <summary>
    /// "The Score" (D-064): everything one correlation id left behind, oldest first: the audited command(s), the outbox event(s) and the webhook
    /// deliveries of those events, with times in milliseconds after the first row. 404 <c>audit.trace_not_found</c> when nothing has that id.
    /// </summary>
    [HttpGet("trace/{correlationId}")]
    [HasPermission(Permissions.AuditRead)]
    [ProducesResponseType(typeof(AuditTraceDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "audit.trace_not_found")]
    public Task<ActionResult<AuditTraceDto>> Trace(string correlationId, CancellationToken ct) =>
        Send(new GetAuditTraceQuery(correlationId), ct);
}
