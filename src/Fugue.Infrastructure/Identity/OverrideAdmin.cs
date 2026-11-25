using System.Text.Json;
using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Overrides;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Identity;

internal sealed class OverrideAdmin(FugueDbContext db, IGrantAuditWriter audit, TimeProvider time) : IOverrideAdmin
{
    public async Task<IReadOnlyList<OverrideDto>> ListAsync(UserId userId, CancellationToken ct)
    {
        DateTimeOffset now = time.GetUtcNow();
        var rows = await db.Overrides.AsNoTracking().Where(o => o.UserId == userId)
            .Join(db.Permissions, o => o.PermissionId, p => p.Id, (o, p) => new { p.Code, o.IsAllowed, o.Reason, o.ExpiresAt })
            .OrderBy(x => x.Code).ToListAsync(ct);
        return rows.Select(r => new OverrideDto(userId, r.Code, r.IsAllowed, r.Reason, r.ExpiresAt, r.ExpiresAt is { } e && e <= now)).ToList();
    }

    public async Task<Result> SetAsync(UserId userId, string permission, bool isAllowed, string? reason, DateTimeOffset? expiresAt, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted, ct))
        {
            return Result.Fail(OverrideErrors.UserNotFound());
        }

        PermissionDefinition? definition = await db.Permissions.AsNoTracking().FirstOrDefaultAsync(p => p.Code == permission, ct);
        if (definition is null)
        {
            return Result.Fail(OverrideErrors.PermissionNotFound(permission));
        }

        UserPermissionOverride? existing = await db.Overrides.FirstOrDefaultAsync(o => o.UserId == userId && o.PermissionId == definition.Id, ct);
        string? before = existing is null ? null : Json(existing);
        if (existing is null)
        {
            existing = new UserPermissionOverride { Id = UserPermissionOverrideId.New(), UserId = userId, PermissionId = definition.Id };
            db.Overrides.Add(existing);
        }

        existing.IsAllowed = isAllowed;
        existing.Reason = reason;
        existing.ExpiresAt = expiresAt;
        if (db.Entry(existing).State == EntityState.Unchanged || db.Entry(existing).State == EntityState.Modified)
        {
            // Last writer wins, and says so: never let EF skip a column because THIS request's stale read already held the value.
            foreach (string column in new[] { nameof(existing.IsAllowed), nameof(existing.Reason), nameof(existing.ExpiresAt) })
            {
                db.Entry(existing).Property(column).IsModified = true;
            }
        }

        audit.Record("permission_override", existing.Id.Value, userId.Value, "granted", reason, before, Json(existing));
        await db.SaveChangesAsync(ct); // the override and its grant_audit row commit together
        return Result.Ok();
    }

    public async Task<Result> RemoveAsync(UserId userId, string permission, CancellationToken ct)
    {
        UserPermissionOverride? existing = await db.Overrides
            .Where(o => o.UserId == userId && db.Permissions.Any(p => p.Id == o.PermissionId && p.Code == permission))
            .FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            return Result.Fail(OverrideErrors.NotFound());
        }

        db.Overrides.Remove(existing);
        audit.Record("permission_override", existing.Id.Value, userId.Value, "revoked", existing.Reason, Json(existing), null);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static string Json(UserPermissionOverride o) =>
        JsonSerializer.Serialize(new { allowed = o.IsAllowed, reason = o.Reason, expiresAt = o.ExpiresAt });
}
