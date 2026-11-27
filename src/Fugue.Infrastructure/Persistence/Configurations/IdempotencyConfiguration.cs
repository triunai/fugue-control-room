using Fugue.Domain.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fugue.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>idempotency_keys</c> (ADR-013). Unique on the scope (actor, endpoint, key), which is what the atomic claim conflicts on; the index on
/// <c>expires_at</c> serves the purge. The response body is ciphertext (bytea), the headers a jsonb object.
/// </summary>
internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_keys", t => t.HasCheckConstraint("ck_idempotency_keys_status", "status IN ('pending', 'completed')"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Endpoint).HasMaxLength(200).IsRequired();
        b.Property(e => e.Key).HasMaxLength(255).IsRequired();
        b.Property(e => e.RequestHash).IsRequired();
        b.Property(e => e.Status).HasMaxLength(16).IsRequired();
        b.Property(e => e.ResponseHeaders).HasColumnType("jsonb");
        b.Property(e => e.CreatedBy).HasMaxLength(64).IsRequired();
        b.Property(e => e.UpdatedBy).HasMaxLength(64).IsRequired();

        b.HasIndex(e => new { e.ActorId, e.Endpoint, e.Key }).IsUnique().HasDatabaseName("ux_idempotency_keys_scope");
        b.HasIndex(e => e.ExpiresAt).HasDatabaseName("ix_idempotency_keys_expires_at");
    }
}
