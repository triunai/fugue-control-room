using System.Text.Json.Serialization;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Claims;

/// <summary>
/// Where an imported claim came from (item 11a). Each is a FILE import standing in for a live connector: a spreadsheet upload for a shared
/// workbook, an <c>.eml</c>/<c>.mbox</c> export for a Microsoft Graph or Gmail inbox, a chat export for WhatsApp Business. No external call is made.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportSource
{
    Spreadsheet,
    Email,
    WhatsApp,
}

/// <summary>
/// The ledger row that makes an import idempotent: one row per imported source item, keyed by the SHA-256 the server computed when it read the file
/// (a spreadsheet row, an email's Message-ID, a chat's first claimant message). Written in the same transaction as the claim, so a second import of
/// the same item fails on the primary key instead of creating a second claim. Rows are never updated.
/// </summary>
public sealed class ClaimImport : IAuditable
{
    private ClaimImport()
    {
        Key = string.Empty;
        FileName = string.Empty;
        FileSha256 = string.Empty;
        Item = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
    }

    /// <summary>Lowercase hex SHA-256 of the source item; the primary key.</summary>
    public string Key { get; private set; }

    public ClaimId ClaimId { get; private set; }

    public ImportSource Source { get; private set; }

    public string FileName { get; private set; }

    /// <summary>Lowercase hex SHA-256 of the uploaded file, as the parse response reported it.</summary>
    public string FileSha256 { get; private set; }

    /// <summary>Which item of the file: "Row 4", "Email 2 of 3: ...", "Chat with ...".</summary>
    public string Item { get; private set; }

    public UserId ImportedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public static ClaimImport Record(string key, ClaimId claimId, ImportSource source, string fileName, string fileSha256, string item, UserId importedBy) => new()
    {
        Key = key,
        ClaimId = claimId,
        Source = source,
        FileName = fileName,
        FileSha256 = fileSha256,
        Item = item,
        ImportedBy = importedBy,
    };
}
