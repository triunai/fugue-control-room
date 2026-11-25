using System.Text.Json;
using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Overrides;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Identity;

internal sealed class OverrideAdmin(FugueDbContext db, IGrantAuditWriter audit, TimeProvider time) : IOverrideAdmin
{
    public async Task<IReadOnlyList<OverrideDto>> ListAsync(UserId userId, CancellationToken ct)
    {
        DateTimeOffset now = time.GetUtcNow();
        var rows = await db.Overrides.AsNoTracking().Where(o => o.UserId == userId)
            .Join(db.Permissions, o => o.PermissionId, p => p.Id, (o, p) => new { p.Code, o.IsAllowed, o.Reason, o.ExpiresAt, o.RowVersion })
            .OrderBy(x => x.Code).ToListAsync(ct);
        return rows.Select(r => new OverrideDto(userId, r.Code, r.IsAllowed, r.Reason, r.ExpiresAt, r.ExpiresAt is { } e && e <= now, r.RowVersion)).ToList();
    }

    public async Task<Result<OverrideDto>> SetAsync(
        UserId userId, string permission, bool isAllowed, string? reason, DateTimeOffset? expiresAt, long? expectedVersion, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted, ct))
        {
            return Result.Fail<OverrideDto>(OverrideErrors.UserNotFound());
        }

        PermissionDefinition? definition = await db.Permissions.AsNoTracking().FirstOrDefaultAsync(p => p.Code == permission, ct);
        if (definition is null)
        {
            return Result.Fail<OverrideDto>(OverrideErrors.PermissionNotFound(permission));
        }

        UserPermissionOverride? existing = await db.Overrides.FirstOrDefaultAsync(o => o.UserId == userId && o.PermissionId == definition.Id, ct);
        string? before = existing is null ? null : Json(existing);
        if (existing is null)
        {
            if (expectedVersion is not null)
            {
                return Result.Fail<OverrideDto>(OverrideErrors.VersionMismatch()); // the caller read a row that is gone
            }

            existing = new UserPermissionOverride { Id = UserPermissionOverrideId.New(), UserId = userId, PermissionId = definition.Id };
            db.Overrides.Add(existing);
        }
        else
        {
            if (expectedVersion is not { } expected)
            {
                return Result.Fail<OverrideDto>(OverrideErrors.IfMatchRequired());
            }

            if (expected != existing.RowVersion)
            {
                return Result.Fail<OverrideDto>(OverrideErrors.VersionMismatch());
            }

            // Pin the version as the EF original value so a write that raced in after the load also fails (WHERE xmin = @expected).
            db.Entry(existing).Property(o => o.RowVersion).OriginalValue = (uint)expected;
        }

        existing.IsAllowed = isAllowed;
        existing.Reason = reason;
        existing.ExpiresAt = expiresAt;
        audit.Record("permission_override", existing.Id.Value, userId.Value, "granted", reason, before, Json(existing));
        try
        {
            await db.SaveChangesAsync(ct); // the override and its grant_audit row commit together
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail<OverrideDto>(OverrideErrors.VersionMismatch());
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Result.Fail<OverrideDto>(OverrideErrors.VersionMismatch()); // a concurrent create of the same (user, permission) won
        }

        return Result.Ok(new OverrideDto(userId, permission, isAllowed, reason, expiresAt, expiresAt is { } e && e <= time.GetUtcNow(), existing.RowVersion));
    }

    public async Task<Result> RemoveAsync(UserId userId, string permission, long? expectedVersion, CancellationToken ct)
    {
        UserPermissionOverride? existing = await db.Overrides
            .Where(o => o.UserId == userId && db.Permissions.Any(p => p.Id == o.PermissionId && p.Code == permission))
            .FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            return Result.Fail(OverrideErrors.NotFound());
        }

        if (expectedVersion is not { } expected)
        {
            return Result.Fail(OverrideErrors.IfMatchRequired());
        }

        if (expected != existing.RowVersion)
        {
            return Result.Fail(OverrideErrors.VersionMismatch());
        }

        db.Entry(existing).Property(o => o.RowVersion).OriginalValue = (uint)expected;
        db.Overrides.Remove(existing);
        audit.Record("permission_override", existing.Id.Value, userId.Value, "revoked", existing.Reason, Json(existing), null);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail(OverrideErrors.VersionMismatch()); // changed or removed since the load
        }

        return Result.Ok();
    }

    private static string Json(UserPermissionOverride o) =>
        JsonSerializer.Serialize(new { allowed = o.IsAllowed, reason = o.Reason, expiresAt = o.ExpiresAt });
}
