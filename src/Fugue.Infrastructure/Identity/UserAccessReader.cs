using Fugue.Application.Abstractions;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Identity;

/// <summary>
/// Effective permissions in ONE statement (plan section 2). Semantics, all in the query:
/// role grants (direct roles, plus roles of active groups the user belongs to; only active roles) plus unexpired allow
/// overrides, minus unexpired deny overrides (deny > allow > role); an override is expired when
/// <c>expires_at &lt;= now</c> (now from <see cref="TimeProvider"/>); permissions under deleted modules, resources or
/// actions, inactive modules, users of an inactive tenant, or modules switched off in configuration are dropped. No cache.
/// </summary>
internal sealed class UserAccessReader(FugueDbContext db, TimeProvider time, IModuleConfig moduleConfig) : IUserAccessReader
{
    public async Task<UserAccess?> GetAsync(UserId userId, CancellationToken ct)
    {
        DateTimeOffset now = time.GetUtcNow();
        string[] disabled = [.. moduleConfig.DisabledCodes()];

        List<Row> rows = await db.Database.SqlQueryRaw<Row>(
            SqlScripts.Get("identity/user-access"),
            new NpgsqlParameter("userId", userId.Value), new NpgsqlParameter("now", now), new NpgsqlParameter("disabled", disabled)).ToListAsync(ct);

        Row? user = rows.FirstOrDefault(r => r.Kind == "u");
        if (user is null)
        {
            return null;
        }

        TenantId? tenant = Guid.TryParse(user.Code, out Guid t) ? TenantId.From(t) : null;
        HashSet<string> permissions = rows.Where(r => r.Kind == "p").Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        return new UserAccess(userId, tenant, permissions);
    }

    private sealed record Row(string Kind, string Code);
}
