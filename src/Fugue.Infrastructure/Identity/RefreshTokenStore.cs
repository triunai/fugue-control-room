using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

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
        db.RefreshTokens.Add(Build(RefreshTokenId.New(), token.UserId, token.FamilyId, token.TokenHash, token.ExpiresAt, token.Client));
        await db.SaveChangesAsync(ct);
    }

    public async Task<RotationResult> RotateAsync(string oldHash, RotationRequest next, DateTimeOffset now, CancellationToken ct)
    {
        RefreshToken? old = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == oldHash, ct);
        if (old is null)
        {
            return new RotationResult(RotationOutcome.NotFound, null);
        }

        IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await LockFamilyAsync(old.FamilyId, ct); // serialize with family revocation: a revoke started later sees our new token
            var newId = RefreshTokenId.New();
            AuditStampValue s = stamp.Capture();
            int claimed = await db.Database.ExecuteSqlRawAsync(
                SqlScripts.Get("identity/rotate-refresh-token"),
                [new NpgsqlParameter("now", now), new NpgsqlParameter("newId", newId.Value), new NpgsqlParameter("stampNow", s.Now), new NpgsqlParameter("actor", s.Actor), new NpgsqlParameter("id", old.Id.Value)],
                ct);

            if (claimed == 1)
            {
                db.RefreshTokens.Add(Build(newId, old.UserId, old.FamilyId, next.NewTokenHash, next.ExpiresAt, next.Client));
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

    private async Task<int> RevokeFamilyCoreAsync(Guid family, DateTimeOffset now, CancellationToken ct)
    {
        IDbContextTransaction? own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await LockFamilyAsync(family, ct); // wait for any in-flight rotation; the UPDATE below then takes a fresh snapshot
            AuditStampValue s = stamp.Capture();
            int revoked = await db.Database.ExecuteSqlRawAsync(
                SqlScripts.Get("identity/revoke-token-family"),
                [new NpgsqlParameter("now", now), new NpgsqlParameter("stampNow", s.Now), new NpgsqlParameter("actor", s.Actor), new NpgsqlParameter("family", family)],
                ct);
            if (own is not null)
            {
                await own.CommitAsync(ct);
            }

            return revoked;
        }
        finally
        {
            if (own is not null)
            {
                await own.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Transaction-scoped advisory lock keyed by the family. Without it, a revoke that starts while a rotation is between its
    /// UPDATE and its INSERT misses the new token (READ COMMITTED snapshot) and the family survives revocation (Codex build review, HIGH).
    /// </summary>
    private Task<int> LockFamilyAsync(Guid family, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(SqlScripts.Get("identity/lock-token-family"), [new NpgsqlParameter("family", family.ToString())], ct);

    private static RefreshToken Build(RefreshTokenId id, UserId user, Guid family, string tokenHash, DateTimeOffset expiresAt, ClientInfo client) => new()
    {
        Id = id,
        UserId = user,
        TokenHash = tokenHash,
        FamilyId = family,
        ExpiresAt = expiresAt,
        CreatedIp = client.Ip,
        CreatedUserAgent = client.UserAgent,
    };
}
