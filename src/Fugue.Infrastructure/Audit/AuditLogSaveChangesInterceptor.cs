using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fugue.Infrastructure.Audit;

/// <summary>
/// EF write path of <c>audit_log</c> (ADR-010). Before SaveChanges it reads the change tracker and ADDS one <see cref="AuditLog"/> row per
/// changed entity to the same context, so the audit rows are saved by the SAME SaveChanges (one transaction, atomic with the change: if the
/// business write fails, no audit row exists). Added rows carry new values, modified rows only the fields whose value really changed
/// (<c>{old,new}</c>), deleted rows the old values. Runs after <c>AuditableSaveChangesInterceptor</c>, whose stamp columns are noise and are skipped.
/// </summary>
internal sealed class AuditLogSaveChangesInterceptor(AuditRowFactory factory, AuditRedactor redactor) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // The audit rows only live inside one SaveChanges. Detach them afterwards: after a FAILED save they describe a change that never
    // happened and a later SaveChanges on the same context must not write them, and after a successful save they only cost memory.
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DetachAuditRows(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        DetachAuditRows(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        DetachAuditRows(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        DetachAuditRows(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static void DetachAuditRows(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (EntityEntry<AuditLog> entry in context.ChangeTracker.Entries<AuditLog>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private void Capture(DbContext? context)
    {
        if (context is null || !factory.Enabled)
        {
            return;
        }

        List<EntityEntry> changed = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && !redactor.IsExcluded(e.Entity.GetType()))
            .ToList();

        foreach (EntityEntry entry in changed)
        {
            Dictionary<string, AuditFieldChange> fields = Diff(entry, out bool unconditional);
            if (fields.Count == 0 && entry.State == EntityState.Modified)
            {
                continue; // touched (for example by the stamp interceptor) but no business column was written
            }

            string table = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name.ToLowerInvariant();
            string verb = entry.State switch
            {
                EntityState.Added => "create",
                EntityState.Deleted => "delete",
                _ => "update",
            };

            context.Add(factory.Create(
                null, $"{table}.{verb}", entry.Metadata.ClrType.Name, KeyOf(entry), AuditOutcomes.Success, null, redactor.Build(entry.Metadata.ClrType, fields),
                unconditional ? new Dictionary<string, object?> { ["unconditionalWrite"] = true } : null));
        }
    }

    /// <summary>
    /// <paramref name="unconditional"/> is true when a column was flagged modified although its value equals the loaded one: code that
    /// forces a write ("last writer wins", as <c>OverrideAdmin</c> does). The database value may have changed since the load, so the
    /// write is audited rather than skipped, and the row says so in its metadata.
    /// </summary>
    private static Dictionary<string, AuditFieldChange> Diff(EntityEntry entry, out bool unconditional)
    {
        unconditional = false;
        var fields = new Dictionary<string, AuditFieldChange>();
        foreach (PropertyEntry property in entry.Properties)
        {
            string name = property.Metadata.Name;
            if (AuditRedactor.NoiseFields.Contains(name) || property.Metadata.IsConcurrencyToken)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added when property.CurrentValue is { } added:
                    fields[name] = AuditFieldChange.Added(added);
                    break;
                case EntityState.Deleted when property.OriginalValue is { } removed:
                    fields[name] = AuditFieldChange.Removed(removed);
                    break;
                case EntityState.Modified when property.IsModified:
                    unconditional |= Equals(property.OriginalValue, property.CurrentValue);
                    fields[name] = AuditFieldChange.Changed(property.OriginalValue, property.CurrentValue);
                    break;
            }
        }

        return fields;
    }

    private static string? KeyOf(EntityEntry entry) =>
        entry.Metadata.FindPrimaryKey() is { } key
            ? string.Join(':', key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()))
            : null;
}
