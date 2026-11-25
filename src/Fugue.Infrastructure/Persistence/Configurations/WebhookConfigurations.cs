using Fugue.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fugue.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>outbox_messages</c> (ADR-011). Status changes are legal, but a trigger (added by the migration) freezes every other column;
/// the partial index serves the relay claim (<c>status = 'pending' AND next_attempt_at &lt;= now</c>).
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages", t => t.HasCheckConstraint("ck_outbox_messages_status", "status IN ('pending', 'dispatched', 'dead')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        b.Property(e => e.AggregateType).HasMaxLength(64).IsRequired();
        b.Property(e => e.AggregateId).HasMaxLength(128).IsRequired();
        b.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(e => e.CorrelationId).HasMaxLength(64);
        b.Property(e => e.SagaId).HasMaxLength(64).IsRequired();
        b.Property(e => e.Traceparent).HasMaxLength(64);
        b.Property(e => e.Status).HasMaxLength(16).IsRequired();
        b.Property(e => e.LastError).HasMaxLength(1000);

        b.HasIndex(e => new { e.NextAttemptAt, e.Id }).HasFilter("status = 'pending'").HasDatabaseName("ix_outbox_messages_pending");
        b.HasIndex(e => new { e.AggregateType, e.AggregateId, e.OccurredAt }).HasDatabaseName("ix_outbox_messages_aggregate");
    }
}

internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> b)
    {
        b.ToTable("webhook_subscriptions");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Name).HasMaxLength(100).IsRequired();
        b.Property(e => e.Url).HasMaxLength(2000).IsRequired();
        b.Property(e => e.EventTypes).HasColumnType("text[]").IsRequired();
        b.Property(e => e.SecretCiphertext).HasMaxLength(2000).IsRequired();
        b.Property(e => e.CreatedBy).HasMaxLength(64).IsRequired();
        b.Property(e => e.UpdatedBy).HasMaxLength(64).IsRequired();
        b.Property(e => e.RowVersion).HasColumnName("xmin").HasColumnType("xid").IsRowVersion(); // system column, never created by migrations

        b.HasIndex(e => new { e.CreatedAt, e.Id }).HasDatabaseName("ix_webhook_subscriptions_created_at_id"); // keyset paging
    }
}

/// <summary>
/// <c>webhook_deliveries</c> (ADR-011): one row per (message, subscription), unique so a repeated fan-out is a no-op. The partial
/// index serves the delivery claim for pending rows and for expired leases.
/// </summary>
internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> b)
    {
        b.ToTable("webhook_deliveries", t => t.HasCheckConstraint("ck_webhook_deliveries_status", "status IN ('pending', 'in_flight', 'succeeded', 'dead')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Status).HasMaxLength(16).IsRequired();
        b.Property(e => e.LastError).HasMaxLength(1000);

        b.HasOne<OutboxMessage>().WithMany().HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<WebhookSubscription>().WithMany().HasForeignKey(e => e.SubscriptionId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(e => new { e.MessageId, e.SubscriptionId }).IsUnique().HasDatabaseName("ux_webhook_deliveries_message_subscription");
        b.HasIndex(e => new { e.NextAttemptAt, e.Id }).HasFilter("status IN ('pending', 'in_flight')").HasDatabaseName("ix_webhook_deliveries_due");
        b.HasIndex(e => new { e.OccurredAt, e.Id }).IsDescending(true, true).HasDatabaseName("ix_webhook_deliveries_occurred_at_id");
        b.HasIndex(e => new { e.SubscriptionId, e.OccurredAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_webhook_deliveries_subscription");
    }
}

internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKeyRecord>
{
    public void Configure(EntityTypeBuilder<DataProtectionKeyRecord> b)
    {
        b.ToTable("data_protection_keys");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.FriendlyName).HasMaxLength(200).IsRequired();
        b.Property(e => e.Xml).IsRequired();
        b.Property(e => e.CreatedBy).HasMaxLength(64).IsRequired();
        b.Property(e => e.UpdatedBy).HasMaxLength(64).IsRequired();
        b.HasIndex(e => e.FriendlyName).IsUnique().HasDatabaseName("ux_data_protection_keys_friendly_name");
    }
}
