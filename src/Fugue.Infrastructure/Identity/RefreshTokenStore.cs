using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fugue.Infrastructure.Identity;

/// <summary>
/// Refresh sessions (<c>refresh_tokens</c>). Only the SHA-256 digest is stored. Rotation claims the old row with ONE conditional
/// UPDATE (<c>revoked_at IS NULL AND expires_at &gt; now</c> and an active user), so of two concurrent refreshes with the same
/// token exactly one wins; the loser is classified as reuse and the family is revoked.
/// </summary>
internal sealed class RefreshTokenStore(FugueDbContext db, IAuditStamp stamp) : IRefreshTokenStore
{
    public async Task CreateAsync(NewRefreshToken token, CancellationToken ct)
    {
        db.RefreshTokens.Add(Build(RefreshTokenId.New(), token.UserId, token.FamilyId, token));
        await db.SaveChangesAsync(ct);
    }

    public async Task<RotationResult> RotateAsync(string oldHash, NewRefreshToken next, DateTimeOffset now, CancellationToken ct)
    {
        RefreshToken? old = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == oldHash, ct);
        if (old is null)
        {
            return new RotationResult(RotationOutcome.NotFound, null);
        }

        IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var newId = RefreshTokenId.New();
            AuditStampValue s = stamp.Capture();
            int claimed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE refresh_tokens SET revoked_at = {now}, replaced_by_id = {newId.Value}, updated_at = {s.Now}, updated_by = {s.Actor}
                WHERE id = {old.Id.Value} AND revoked_at IS NULL AND expires_at > {now}
                  AND EXISTS (SELECT 1 FROM users u WHERE u.id = refresh_tokens.user_id AND u.is_active AND NOT u.is_deleted)
                """, ct);

            if (claimed == 1)
            {
                db.RefreshTokens.Add(Build(newId, old.UserId, old.FamilyId, next));
                await db.SaveChangesAsync(ct);
                if (own is not null)
                {
                    await own.CommitAsync(ct);
                }

                return new RotationResult(RotationOutcome.Rotated, old.UserId);
            }

            RotationOutcome outcome;
            if (old.RevokedAt is not null || await db.RefreshTokens.AsNoTracking().AnyAsync(t => t.Id == old.Id && t.RevokedAt != null, ct))
            {
                outcome = RotationOutcome.Reused;
                await RevokeFamilyCoreAsync(old.FamilyId, now, ct); // theft response: nothing in this family works any more
            }
            else if (old.ExpiresAt <= now)
            {
                outcome = RotationOutcome.Expired;
            }
            else
            {
                outcome = RotationOutcome.UserInactive;
                await RevokeFamilyCoreAsync(old.FamilyId, now, ct);
            }

            if (own is not null)
            {
                await own.CommitAsync(ct);
            }

            return new RotationResult(outcome, null);
        }
        finally
        {
            if (own is not null)
            {
                await own.DisposeAsync();
            }
        }
    }

    public async Task RevokeFamilyAsync(string tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        Guid? family = await db.RefreshTokens.AsNoTracking().Where(t => t.TokenHash == tokenHash).Select(t => (Guid?)t.FamilyId).FirstOrDefaultAsync(ct);
        if (family is { } id)
        {
            await RevokeFamilyCoreAsync(id, now, ct);
        }
    }

    private Task<int> RevokeFamilyCoreAsync(Guid family, DateTimeOffset now, CancellationToken ct)
    {
        AuditStampValue s = stamp.Capture();
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE refresh_tokens SET revoked_at = {now}, updated_at = {s.Now}, updated_by = {s.Actor} WHERE family_id = {family} AND revoked_at IS NULL", ct);
    }

    private static RefreshToken Build(RefreshTokenId id, UserId user, Guid family, NewRefreshToken t) => new()
    {
        Id = id,
        UserId = user,
        TokenHash = t.TokenHash,
        FamilyId = family,
        ExpiresAt = t.ExpiresAt,
        CreatedIp = t.Client.Ip,
        CreatedUserAgent = t.Client.UserAgent,
    };
}
