using FluentResults;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Import;

/// <summary>A live account an imported claim can belong to. <paramref name="CanOwnClaims"/>: its effective permissions include <c>CLAIMS_SUBMIT</c>.</summary>
public sealed record ImportClaimant(UserId Id, string DisplayName, string Email, bool CanOwnClaims);

/// <summary>An item of a file that was already imported, and the claim it became.</summary>
public sealed record ImportedRef(string Key, ClaimId ClaimId, string ClaimNumber);

/// <summary>Persistence for the import slice (ADR-006): EF reads, the ledger insert through Dapper in the caller's unit of work.</summary>
public interface IImportStore
{
    /// <summary>A live, active user by canonical email (<c>EmailKey</c>), with whether they may own claims; null when there is none.</summary>
    Task<ImportClaimant?> FindClaimantAsync(string emailKey, CancellationToken ct);

    /// <summary>The ledger rows among <paramref name="keys"/>, keyed by their key.</summary>
    Task<IReadOnlyDictionary<string, ImportedRef>> FindImportedAsync(IReadOnlyCollection<string> keys, CancellationToken ct);

    /// <summary>Inserts the ledger row and its audit row in the current transaction; <c>import.already_imported</c> (Conflict) when the key exists.</summary>
    Task<Result> AddAsync(ClaimImport import, CancellationToken ct);

    /// <summary>The newest ledger rows first, with the claim number and status and who imported them.</summary>
    Task<IReadOnlyList<ImportedClaimDto>> ListRecentAsync(int take, CancellationToken ct);
}

/// <summary>The domain-to-market table (<c>Import:MarketByDomain</c>, bound with IOptions in Infrastructure) the inbox import routes senders with.</summary>
public interface IImportSettings
{
    IReadOnlyDictionary<string, string> MarketByDomain { get; }
}
