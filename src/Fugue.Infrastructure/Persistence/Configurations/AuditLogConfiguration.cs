using Fugue.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fugue.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>audit_log</c> (ADR-010). Append-only: the migration adds a trigger that rejects UPDATE, DELETE and TRUNCATE. Every
/// index leads with the filter column and ends with <c>occurred_at DESC, id DESC</c>, the keyset order of the read API.
/// </summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log", t =>
        {
            t.HasCheckConstraint("ck_audit_log_actor_type", "actor_type IN ('user', 'system', 'service')");
            t.HasCheckConstraint("ck_audit_log_outcome", "outcome IN ('success', 'failure')");
        });
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.ActorType).HasMaxLength(16).IsRequired();
        b.Property(e => e.ActorDisplay).HasMaxLength(200);
        b.Property(e => e.PermissionUsed).HasMaxLength(256);
        b.Property(e => e.Action).HasMaxLength(64).IsRequired();
        b.Property(e => e.EntityType).HasMaxLength(64);
        b.Property(e => e.EntityId).HasMaxLength(128);
        b.Property(e => e.Outcome).HasMaxLength(16).IsRequired();
        b.Property(e => e.ErrorCode).HasMaxLength(128);
        b.Property(e => e.ErrorType).HasColumnType("text");
        b.Property(e => e.ErrorMessage).HasColumnType("text");
        b.Property(e => e.DurationMs).HasColumnType("integer");
        b.Property(e => e.Changes).HasColumnType("jsonb");
        b.Property(e => e.CorrelationId).HasMaxLength(64);
        b.Property(e => e.SagaId).HasMaxLength(64);
        b.Property(e => e.TraceId).HasMaxLength(32);
        b.Property(e => e.HttpMethod).HasMaxLength(8);
        b.Property(e => e.RouteTemplate).HasMaxLength(256);
        b.Property(e => e.ClientIp).HasMaxLength(64);
        b.Property(e => e.UserAgent).HasMaxLength(256);
        b.Property(e => e.Metadata).HasColumnType("jsonb");

        b.HasIndex(e => new { e.OccurredAt, e.Id }).IsDescending(true, true).HasDatabaseName("ix_audit_log_occurred_at_id");
        b.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAt, e.Id }).IsDescending(false, false, true, true).HasDatabaseName("ix_audit_log_entity");
        b.HasIndex(e => new { e.ActorUserId, e.OccurredAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_audit_log_actor");
        b.HasIndex(e => new { e.Action, e.OccurredAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_audit_log_action");
        b.HasIndex(e => e.CorrelationId).HasDatabaseName("ix_audit_log_correlation_id");
    }
}
