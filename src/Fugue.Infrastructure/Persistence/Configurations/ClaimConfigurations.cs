using Fugue.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fugue.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>claims</c> (W-020, D-053). Money columns are bigint minor units with a positive-value CHECK; the currency is fixed by the market. Three indexes serve the
/// three access paths and each ends in the keyset order of its list: the staff queue, the claimant's own list and the exposure roll-up.
/// </summary>
internal sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> b)
    {
        b.ToTable("claims", t =>
        {
            t.HasCheckConstraint("ck_claims_reserve_positive", "reserve_minor IS NULL OR reserve_minor > 0");
            t.HasCheckConstraint("ck_claims_approved_positive", "approved_amount_minor IS NULL OR approved_amount_minor > 0");
        });
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever(); // UUIDv7 from the domain

        b.Property(c => c.Number).HasMaxLength(32).IsRequired();
        b.Property(c => c.Market).HasConversion<string>().HasMaxLength(2).IsRequired();
        b.Property(c => c.Currency).HasMaxLength(3).IsRequired();
        b.Property(c => c.Line).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(c => c.PolicyNumber).HasMaxLength(64).IsRequired();
        b.Property(c => c.IncidentDescription).HasMaxLength(4000).IsRequired();
        b.Property(c => c.IncidentLocation).HasMaxLength(200);
        b.Property(c => c.DecisionReason).HasMaxLength(2000);
        b.Property(c => c.CreatedBy).HasMaxLength(64).IsRequired();
        b.Property(c => c.UpdatedBy).HasMaxLength(64).IsRequired();

        // Postgres xmin as the optimistic-concurrency token (ADR-006): the ETag / If-Match version.
        b.Property(c => c.RowVersion).HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        b.HasOne<Fugue.Domain.Identity.User>().WithMany().HasForeignKey(c => c.ClaimantUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Fugue.Domain.Identity.User>().WithMany().HasForeignKey(c => c.AssignedOfficerId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(c => c.Timeline).WithOne().HasForeignKey(e => e.ClaimId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(c => c.Timeline).HasField("_timeline");
        b.HasMany(c => c.InfoRequests).WithOne().HasForeignKey(r => r.ClaimId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(c => c.InfoRequests).HasField("_infoRequests");
        b.Ignore(c => c.OpenInfoRequest);

        b.HasIndex(c => c.Number).IsUnique().HasDatabaseName("ux_claims_number");
        b.HasIndex(c => new { c.Status, c.AssignedOfficerId, c.SubmittedAt, c.Id }).HasDatabaseName("ix_claims_queue");
        b.HasIndex(c => new { c.ClaimantUserId, c.SubmittedAt, c.Id }).IsDescending(false, true, true).HasDatabaseName("ix_claims_claimant");
        b.HasIndex(c => new { c.Status, c.Market, c.Line }).HasDatabaseName("ix_claims_exposure");
    }
}

/// <summary>
/// <c>claim_timeline</c>: append-only. The migration adds a trigger that rejects UPDATE, DELETE and TRUNCATE, the same guard as <c>audit_log</c>.
/// </summary>
internal sealed class ClaimTimelineEntryConfiguration : IEntityTypeConfiguration<ClaimTimelineEntry>
{
    public void Configure(EntityTypeBuilder<ClaimTimelineEntry> b)
    {
        b.ToTable("claim_timeline");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.ToStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(e => e.ActorDisplay).HasMaxLength(200).IsRequired();
        b.Property(e => e.Note).HasMaxLength(4100).IsRequired();
        b.Property(e => e.CreatedBy).HasMaxLength(64).IsRequired();
        b.Property(e => e.UpdatedBy).HasMaxLength(64).IsRequired();
        b.HasOne<Fugue.Domain.Identity.User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.ClaimId, e.OccurredAt, e.Id }).HasDatabaseName("ix_claim_timeline_claim");
    }
}

/// <summary><c>claim_info_requests</c>: at most one OPEN (unanswered) request per claim, enforced by a partial unique index.</summary>
internal sealed class ClaimInfoRequestConfiguration : IEntityTypeConfiguration<ClaimInfoRequest>
{
    public void Configure(EntityTypeBuilder<ClaimInfoRequest> b)
    {
        b.ToTable("claim_info_requests");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Question).HasMaxLength(2000).IsRequired();
        b.Property(r => r.Response).HasMaxLength(4000);
        b.Property(r => r.CreatedBy).HasMaxLength(64).IsRequired();
        b.Property(r => r.UpdatedBy).HasMaxLength(64).IsRequired();
        b.Ignore(r => r.IsOpen);
        b.HasOne<Fugue.Domain.Identity.User>().WithMany().HasForeignKey(r => r.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.ClaimId, r.RequestedAt }).HasDatabaseName("ix_claim_info_requests_claim");
        b.HasIndex(r => r.ClaimId).IsUnique().HasFilter("responded_at IS NULL").HasDatabaseName("ux_claim_info_requests_open");
    }
}
