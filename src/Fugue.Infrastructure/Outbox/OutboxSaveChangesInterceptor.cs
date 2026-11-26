using Fugue.Domain.Events;
using Fugue.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fugue.Infrastructure.Outbox;

/// <summary>
/// EF write path of the outbox (ADR-011), the sibling of <c>AuditLogSaveChangesInterceptor</c>. Before SaveChanges it drains the domain events
/// of every changed aggregate and ADDS one <see cref="OutboxMessage"/> per event to the same context, so the SAME SaveChanges (one transaction)
/// writes the change and its events: a failed save leaves neither, a rolled-back unit of work removes both. After a successful save the
/// aggregates' events are cleared; after a failed or cancelled one they are kept (the rows are detached either way) so a retry on the same context re-captures them.
/// </summary>
internal sealed class OutboxSaveChangesInterceptor(OutboxRowFactory factory) : SaveChangesInterceptor
{
    private readonly List<IHasDomainEvents> _captured = [];

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

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Complete(eventData.Context, success: true);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Complete(eventData.Context, success: true);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Complete(eventData.Context, success: false);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Complete(eventData.Context, success: false);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        Complete(eventData.Context, success: false);
        base.SaveChangesCanceled(eventData);
    }

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        Complete(eventData.Context, success: false);
        return base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        _captured.Clear();
        List<IHasDomainEvents> aggregates = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        foreach (IHasDomainEvents aggregate in aggregates)
        {
            foreach (IDomainEvent evt in aggregate.DomainEvents)
            {
                context.Add(factory.Create(evt));
            }

            _captured.Add(aggregate);
        }
    }

    private void Complete(DbContext? context, bool success)
    {
        if (context is not null)
        {
            foreach (EntityEntry<OutboxMessage> entry in context.ChangeTracker.Entries<OutboxMessage>().ToList())
            {
                entry.State = EntityState.Detached; // only live inside one SaveChanges
            }
        }

        if (success)
        {
            foreach (IHasDomainEvents aggregate in _captured)
            {
                aggregate.ClearDomainEvents();
            }
        }

        _captured.Clear();
    }
}
