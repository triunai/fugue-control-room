using Fugue.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fugue.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>claim_imports</c> (item 11a): the idempotency ledger of file imports. The key (a SHA-256 of the source item) is the primary key, so a second
/// import of the same item fails inside the claim's transaction; one claim has at most one import row. FKs are RESTRICT like the claims tables.
/// </summary>
internal sealed class ClaimImportConfiguration : IEntityTypeConfiguration<ClaimImport>
{
    public void Configure(EntityTypeBuilder<ClaimImport> b)
    {
        b.ToTable("claim_imports", t =>
        {
            t.HasCheckConstraint("ck_claim_imports_key_sha256", "key ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_claim_imports_source", "source IN ('Spreadsheet', 'Email', 'WhatsApp')");
        });
        b.HasKey(i => i.Key);
        b.Property(i => i.Key).HasMaxLength(64).ValueGeneratedNever();
        b.Property(i => i.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(i => i.FileName).HasMaxLength(255).IsRequired();
        b.Property(i => i.FileSha256).HasMaxLength(64).IsRequired();
        b.Property(i => i.Item).HasMaxLength(200).IsRequired();
        b.Property(i => i.CreatedBy).HasMaxLength(64).IsRequired();
        b.Property(i => i.UpdatedBy).HasMaxLength(64).IsRequired();

        b.HasOne<Claim>().WithMany().HasForeignKey(i => i.ClaimId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Fugue.Domain.Identity.User>().WithMany().HasForeignKey(i => i.ImportedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(i => i.ClaimId).IsUnique().HasDatabaseName("ux_claim_imports_claim");
        b.HasIndex(i => new { i.CreatedAt, i.Key }).IsDescending(true, true).HasDatabaseName("ix_claim_imports_created_at");
        b.HasIndex(i => i.ImportedBy).HasDatabaseName("ix_claim_imports_imported_by");
    }
}
