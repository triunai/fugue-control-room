using System.Security.Cryptography;
using System.Text;
using FluentResults;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Import.Models;

/// <summary>
/// One claim a person still has to review, as read from a file. Every field is the raw text the parser found (null when it found nothing); the
/// check query normalises and validates it with the Report an incident rules. <paramref name="Key"/> is the server's content hash of the source item
/// (a spreadsheet row, an email, a chat) and is what makes an import idempotent. <paramref name="Hints"/> say how a value was inferred, so the
/// reviewer knows what to double-check. <paramref name="Note"/> is attached to the claim's timeline when it is created (the chat transcript, the
/// email header).
/// </summary>
public sealed record ImportDraft(
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
    IReadOnlyList<string> Hints);

/// <summary>A spreadsheet as read: the header row and the non-empty data rows (cells as text; date cells of an .xlsx already as yyyy-MM-dd).</summary>
public sealed record ImportSheet(IReadOnlyList<string> Headers, IReadOnlyList<ImportSheetRow> Rows);

/// <summary>One data row. <paramref name="Number"/> is the row number a person sees in the spreadsheet (the header is row 1).</summary>
public sealed record ImportSheetRow(int Number, string Key, IReadOnlyList<string> Cells);

/// <summary>Limits that keep a hostile or accidental file from exhausting the server.</summary>
public static class ImportLimits
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxRows = 2000;
    public const int MaxColumns = 50;
    public const int MaxCellChars = 8000;
    public const int MaxMessages = 200;

    /// <summary>The ceiling on what one decompressed part of an .xlsx may expand to (a zip bomb stops here).</summary>
    public const long MaxXlsxPartBytes = 32L * 1024 * 1024;

    /// <summary>The claim timeline note column holds 4100; the transcript is cut to leave room for the provenance line.</summary>
    public const int MaxNoteChars = 3600;

    public const int MaxDescriptionChars = 4000;
}

internal static class ImportText
{
    /// <summary>Lowercase hex SHA-256 of <paramref name="text"/> (UTF-8).</summary>
    public static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>The file could not be read as the kind it claims to be. A 400 with the reason on the <c>file</c> field.</summary>
    public static Result<T> Unreadable<T>(string message) =>
        Result.Fail<T>(FugueErrors.Validation("import.unreadable", message, new Dictionary<string, string[]> { ["file"] = [message] }));

    /// <summary>Decodes text bytes: a UTF-8 or UTF-16 byte-order mark wins, otherwise strict UTF-8, falling back to Windows-1252-ish Latin-1.</summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    /// <summary>True when the bytes look like a binary file (a NUL in the first 4 KB), which no text import accepts.</summary>
    public static bool LooksBinary(byte[] bytes) => bytes.AsSpan(0, Math.Min(bytes.Length, 4096)).IndexOf((byte)0) >= 0;

    public static string Cap(string text, int max) => text.Length <= max ? text : text[..max];
}
