using Fugue.Application.Abstractions;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Audit;

/// <summary>Looks up a user's display name for the audit snapshot. One no-tracking query per command, before the handler runs.</summary>
internal sealed class ActorDirectory(FugueDbContext db) : IActorDirectory
{
    public Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == UserId.From(userId)).Select(u => (string?)u.DisplayName).FirstOrDefaultAsync(ct);
}
