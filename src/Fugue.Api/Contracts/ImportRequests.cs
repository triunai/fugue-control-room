using Fugue.Application.Features.Import.Models;
using Fugue.Domain.Claims;

namespace Fugue.Api.Contracts;

/// <summary>Body of a draft check: the drafts as the reviewer has them, raw text, in the order to answer in.</summary>
public sealed record CheckImportDraftsRequest(IReadOnlyList<ImportDraftInput> Drafts);

/// <summary>
/// Body of a create from a reviewed draft. <c>Key</c>, <c>FileName</c>, <c>FileSha256</c> and <c>Item</c> are echoed from the parse response; the claim
/// fields are the normalised values from the check; <c>ClaimantEmail</c> names the account the claim belongs to; <c>Note</c> goes on the timeline.
/// </summary>
public sealed record CreateImportedClaimRequest(
    ImportSource Source,
    string Key,
    string FileName,
    string FileSha256,
    string Item,
    Market Market,
    LineOfBusiness Line,
    string PolicyNumber,
    DateOnly IncidentDate,
    string IncidentDescription,
    string? IncidentLocation,
    string ClaimantEmail,
    string? Note = null);

internal static class ImportUpload
{
    /// <summary>The upload's bytes, at most one byte past the limit (enough for the validator to say it is too large without buffering more).</summary>
    public static async Task<byte[]> BytesAsync(IFormFile file, CancellationToken ct)
    {
        await using Stream stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;
        while (buffer.Length <= ImportLimits.MaxFileBytes && (read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
