using Fugue.Application.Features.Import.Parsing;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Import;

/// <summary>
/// What the server read from an uploaded file. A spreadsheet comes back as <see cref="Sheet"/> (the reviewer maps its columns in the browser);
/// an inbox export or a chat comes back as <see cref="Drafts"/>. <see cref="FileSha256"/> identifies the upload; each row or draft carries its
/// own key and, when that item was imported before, the claim it became.
/// </summary>
public sealed record ImportFileDto(
    ImportSource Source,
    string FileName,
    string FileSha256,
    ImportSheetDto? Sheet,
    IReadOnlyList<ImportDraftDto> Drafts);

public sealed record ImportSheetDto(IReadOnlyList<string> Headers, IReadOnlyList<ImportSheetRowDto> Rows);

/// <summary>One data row; <see cref="Number"/> is the row number in the spreadsheet (the header is row 1).</summary>
public sealed record ImportSheetRowDto(int Number, string Key, IReadOnlyList<string> Cells, ImportedRefDto? Imported);

/// <summary>A draft read from an email or a chat: raw text for each field (null when nothing was found) and how each value was inferred.</summary>
public sealed record ImportDraftDto(
    string Key,
    string Item,
    string? Market,
    string? Line,
    string? PolicyNumber,
    string? IncidentDate,
    string? IncidentDescription,
    string? IncidentLocation,
    string? ClaimantEmail,
    string? ClaimantName,
    string? Note,
    IReadOnlyList<string> Hints,
    ImportedRefDto? Imported)
{
    internal static ImportDraftDto From(ImportDraft d, ImportedRefDto? imported) => new(
        d.Key, d.Item, d.Market, d.Line, d.PolicyNumber, d.IncidentDate, d.IncidentDescription, d.IncidentLocation, d.ClaimantEmail, d.ClaimantName,
        d.Note, d.Hints, imported);
}

/// <summary>The claim an item already became.</summary>
public sealed record ImportedRefDto(ClaimId ClaimId, string ClaimNumber);

/// <summary>
/// A draft as the reviewer has it (raw text, possibly edited), sent back to be checked. Market accepts the code or the country name, line is
/// case-insensitive, the date accepts yyyy-mm-dd or day-first d/m/yyyy.
/// </summary>
public sealed record ImportDraftInput(
    string Key,
    string? Market,
    string? Line,
    string? PolicyNumber,
    string? IncidentDate,
    string? IncidentDescription,
    string? IncidentLocation,
    string? ClaimantEmail);

/// <summary>
/// The verdict on one draft. <see cref="Ready"/>: it passes the Report an incident rules, the claimant account exists and may own claims, and the item
/// was not imported before. <see cref="Errors"/> are keyed by field (camelCase), with the same messages Report an incident shows.
/// The normalised values are what to send to create.
/// </summary>
public sealed record ImportCheckDto(
    string Key,
    bool Ready,
    Market? Market,
    LineOfBusiness? Line,
    DateOnly? IncidentDate,
    IReadOnlyDictionary<string, string[]> Errors,
    ImportClaimantDto? Claimant,
    ImportedRefDto? Imported);

public sealed record ImportClaimantDto(UserId Id, string DisplayName);

/// <summary>One imported claim, newest first, for the Import page's "Imported" list.</summary>
public sealed record ImportedClaimDto(
    ClaimId ClaimId,
    string ClaimNumber,
    ClaimStatus Status,
    ImportSource Source,
    string FileName,
    string Item,
    string ImportedBy,
    DateTimeOffset ImportedAt);
