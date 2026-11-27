using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Claims;
using Fugue.Application.Features.Import;
using Fugue.Application.Features.Import.Commands;
using Fugue.Application.Features.Import.Queries;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>
/// Intake import (item 11a): staff turn a spreadsheet, an inbox export (.eml/.mbox) or a WhatsApp chat export into DRAFT claims, review them, and create
/// claims one by one through the normal submit path. These are FILE imports standing in for live Microsoft Graph, Gmail and WhatsApp Business
/// connectors: no external service is called. Staff only (<c>CLAIMS_HANDLE</c>); idempotent by the item's content key; every create is audited.
/// </summary>
[Module(ModuleCodes.Claims)]
public sealed class ImportController : ApiControllerBase
{
    private const long MaxUploadBytes = 3 * 1024 * 1024;

    /// <summary>
    /// Read an uploaded file (multipart field `file`) as `source`: Spreadsheet (.csv, .xlsx) returns the header and rows for column mapping; Email
    /// (.eml, .mbox) and WhatsApp (chat export .txt) return drafts. Nothing is stored. Items imported before carry the claim they became. 400
    /// `import.unreadable` when the file is not what the source expects.
    /// </summary>
    [HttpPost("files")]
    [HasPermission(Permissions.ClaimsHandle)]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    [ProducesResponseType(typeof(ImportFileDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status400BadRequest, "import.unreadable", "validation.failed")]
    public async Task<ActionResult<ImportFileDto>> ReadFile([FromQuery] ImportSource source, IFormFile file, CancellationToken ct) =>
        await Send(new ReadImportFileQuery(source, file.FileName, await ImportUpload.BytesAsync(file, ct)), ct);

    /// <summary>
    /// Check drafts against the Report an incident rules (the same validator), the claimant account and the import ledger. Nothing is created; every
    /// draft gets a verdict with field errors, the normalised market, line and date, the claimant found and the claim an earlier import became.
    /// </summary>
    [HttpPost("check")]
    [HasPermission(Permissions.ClaimsHandle)]
    [ProducesResponseType(typeof(IReadOnlyList<ImportCheckDto>), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status400BadRequest, "validation.failed")]
    public Task<ActionResult<IReadOnlyList<ImportCheckDto>>> Check(CheckImportDraftsRequest body, CancellationToken ct) =>
        Send(new CheckImportDraftsQuery(body.Drafts), ct);

    /// <summary>
    /// Create the claim from one reviewed draft, owned by the claimant account named by `claimantEmail` and submitted the normal way (Submitted, timeline,
    /// audit, `claim.submitted` event), with an import note on its timeline. 409 `import.already_imported` when the item's key was imported before.
    /// Not `[Idempotent]` on purpose: the item's key in the import ledger already makes a retry harmless (409 naming the claim it became).
    /// </summary>
    [HttpPost("claims")]
    [HasPermission(Permissions.ClaimsHandle)]
    [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status201Created)]
    [ProblemResponse(StatusCodes.Status400BadRequest, "validation.failed", "import.claimant_unknown", "import.claimant_cannot_own_claims")]
    [ProblemResponse(StatusCodes.Status409Conflict, ImportErrors.AlreadyImportedCode)]
    public Task<ActionResult<ClaimDto>> Create(CreateImportedClaimRequest body, CancellationToken ct) =>
        SendCreated(
            new CreateImportedClaimCommand(
                body.Source, body.Key, body.FileName, body.FileSha256, body.Item, body.Market, body.Line, body.PolicyNumber, body.IncidentDate,
                body.IncidentDescription, body.IncidentLocation, body.ClaimantEmail, body.Note),
            nameof(ClaimsController.Get), c => new { controller = "Claims", id = c.Id }, ct); // Location: the claim itself

    /// <summary>The most recent imports, newest first (`limit` 1 to 50, default 20): the claim, its status, the file and item, who imported it and when.</summary>
    [HttpGet("claims")]
    [HasPermission(Permissions.ClaimsHandle)]
    [ProducesResponseType(typeof(IReadOnlyList<ImportedClaimDto>), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status400BadRequest, "validation.failed")]
    public Task<ActionResult<IReadOnlyList<ImportedClaimDto>>> Imported([FromQuery] int? limit, CancellationToken ct) =>
        Send(new ListImportedClaimsQuery(limit), ct);
}
