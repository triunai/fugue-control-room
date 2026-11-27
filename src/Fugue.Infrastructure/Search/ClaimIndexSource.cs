using Fugue.Domain.Claims;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// Read side for the claim indexer: <c>claims</c> by last change, oldest first, keyset after an <c>(updated_at, id)</c> checkpoint, no tracking. Claims are
/// updated in place, so a changed row comes round again with a newer <c>updated_at</c> and is re-upserted.
/// </summary>
internal sealed class ClaimIndexSource(FugueDbContext db)
{
    public async Task<IReadOnlyList<ClaimIndexRow>> NextBatchAsync(DateTimeOffset? afterUpdatedAt, Guid? afterId, int take, CancellationToken ct)
    {
        IQueryable<Claim> claims = afterUpdatedAt is { } at && afterId is { } id
            ? db.Claims.FromSqlRaw(SqlScripts.Get("claims/index-batch"), new NpgsqlParameter("updatedAt", at), new NpgsqlParameter("id", id))
            : db.Claims;

        var rows = await (
            from c in claims.AsNoTracking()
            join u in db.Users on c.ClaimantUserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            orderby c.UpdatedAt, c.Id
            select new { Claim = c, Name = u == null ? null : u.DisplayName })
            .Take(take).ToListAsync(ct);

        return [.. rows.Select(r => new ClaimIndexRow(r.Claim, r.Name))];
    }
}
