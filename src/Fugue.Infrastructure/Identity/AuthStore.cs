using Fugue.Application.Abstractions;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Identity;

internal sealed class AuthStore(FugueDbContext db) : IAuthStore
{
    public Task<LoginUser?> FindLoginUserAsync(string emailKey, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(u => u.Email == emailKey && !u.IsDeleted) // deleted rows never authenticate (Codex finding 6)
            .Select(u => new LoginUser(u.Id, u.PasswordHash, u.IsActive, u.LocalLoginEnabled))
            .FirstOrDefaultAsync(ct);
}
