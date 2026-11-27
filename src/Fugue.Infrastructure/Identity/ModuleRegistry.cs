using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Modules.Models;
using Fugue.Application.Features.Modules.Repos;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fugue.Infrastructure.Identity;

internal sealed class ModuleRegistry(FugueDbContext db, IModuleConfig config) : IModuleRegistry
{
    public async Task<IReadOnlyList<ModuleDto>> ListAsync(CancellationToken ct)
    {
        IReadOnlySet<string> off = config.DisabledCodes();
        List<Module> modules = await db.Modules.AsNoTracking().Where(m => !m.IsDeleted).OrderBy(m => m.Code).ToListAsync(ct);
        return modules.Select(m => ToDto(m, off)).ToList();
    }

    public async Task<bool> IsEffectivelyEnabledAsync(string code, CancellationToken ct)
    {
        string key = code.ToUpperInvariant();
        return !config.DisabledCodes().Contains(key)
            && await db.Modules.AsNoTracking().AnyAsync(m => m.Code == key && m.IsActive && !m.IsDeleted, ct);
    }

    public async Task<Result<ModuleDto>> SetActiveAsync(string code, bool active, long expectedVersion, CancellationToken ct)
    {
        Module? module = await db.Modules.FirstOrDefaultAsync(m => m.Code == code && !m.IsDeleted, ct);
        if (module is null)
        {
            return Result.Fail<ModuleDto>(ModuleErrors.NotFound(code));
        }

        if (expectedVersion != module.RowVersion)
        {
            return Result.Fail<ModuleDto>(ModuleErrors.VersionMismatch());
        }

        db.Entry(module).Property(m => m.RowVersion).OriginalValue = (uint)expectedVersion;
        module.IsActive = active;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail<ModuleDto>(ModuleErrors.VersionMismatch());
        }

        return Result.Ok(ToDto(module, config.DisabledCodes()));
    }

    private static ModuleDto ToDto(Module m, IReadOnlySet<string> off)
    {
        bool configEnabled = !off.Contains(m.Code);
        return new ModuleDto(m.Code, m.Name, configEnabled, m.IsActive, configEnabled && m.IsActive, m.RowVersion);
    }
}
