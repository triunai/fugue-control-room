using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Import;
using Fugue.Application.Features.Import.Commands;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Persistence.Import;

/// <summary>
/// ADR-006 for the import slice: EF reads (claimant lookup, ledger lookups, the recent list) and one Dapper insert of the ledger row with its audit row,
/// in the caller's unit of work. The claimant's effective permissions come from the same per-request reader the authorization handler uses.
/// </summary>
internal sealed class ImportStore(FugueDbContext db, SqlExecutor sql, IAuditStamp stamp, IAuditTrail audit, IUserAccessReader access) : IImportStore
{
    private static readonly string InsertSql = SqlScripts.Get("import/insert");

    public async Task<ImportClaimant?> FindClaimantAsync(string emailKey, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Email == emailKey && !u.IsDeleted && u.IsActive)
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .FirstOrDefaultAsync(ct);
        if (user is null)
        {
            return null;
        }

        UserAccess? granted = await access.GetAsync(user.Id, ct);
        return new ImportClaimant(user.Id, user.DisplayName, user.Email, granted?.Permissions.Contains(Permissions.ClaimsSubmit) == true);
    }

    public async Task<IReadOnlyDictionary<string, ImportedRef>> FindImportedAsync(IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<string, ImportedRef>();
        }

        string[] wanted = [.. keys];
        return await (from i in db.ClaimImports.AsNoTracking()
                      join c in db.Claims.AsNoTracking() on i.ClaimId equals c.Id
                      where wanted.Contains(i.Key)
                      select new ImportedRef(i.Key, c.Id, c.Number))
            .ToDictionaryAsync(r => r.Key, StringComparer.Ordinal, ct);
    }

    public async Task<Result> AddAsync(ClaimImport import, CancellationToken ct)
    {
        AuditStampValue now = stamp.Capture();
        import.CreatedAt = import.UpdatedAt = now.Now;
        import.CreatedBy = import.UpdatedBy = now.Actor;
        int inserted = await sql.ExecuteAsync(
            InsertSql,
            new
            {
                import.Key,
                import.ClaimId,
                Source = import.Source.ToString(),
                import.FileName,
                import.FileSha256,
                import.Item,
                import.ImportedBy,
                now.Now,
                now.Actor,
            },
            ct);
        if (inserted == 0)
        {
            string number = (await FindImportedAsync([import.Key], ct)).TryGetValue(import.Key, out ImportedRef? r) ? r.ClaimNumber : "an earlier claim";
            return Result.Fail(ImportErrors.AlreadyImported(number));
        }

        await audit.RecordAsync(AuditSnapshot.Created(import, import.Key, "import.create"), ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<ImportedClaimDto>> ListRecentAsync(int take, CancellationToken ct) =>
        await (from i in db.ClaimImports.AsNoTracking()
               join c in db.Claims.AsNoTracking() on i.ClaimId equals c.Id
               join u in db.Users.AsNoTracking() on i.ImportedBy equals u.Id
               orderby i.CreatedAt descending, i.Key descending
               select new ImportedClaimDto(c.Id, c.Number, c.Status, i.Source, i.FileName, i.Item, u.DisplayName, i.CreatedAt))
            .Take(take)
            .ToListAsync(ct);
}
