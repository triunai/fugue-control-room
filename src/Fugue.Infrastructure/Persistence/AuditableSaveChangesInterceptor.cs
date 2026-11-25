using Fugue.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fugue.Infrastructure.Persistence;

/// <summary>EF write path of audit stamping. The Dapper path binds <see cref="IAuditStamp"/> values in its SQL.</summary>
internal sealed class AuditableSaveChangesInterceptor(IAuditStamp stamp) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        AuditStampValue now = stamp.Capture();
        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now.Now;
                    entry.Entity.CreatedBy = now.Actor;
                    entry.Entity.UpdatedAt = now.Now;
                    entry.Entity.UpdatedBy = now.Actor;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now.Now;
                    entry.Entity.UpdatedBy = now.Actor;
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}
