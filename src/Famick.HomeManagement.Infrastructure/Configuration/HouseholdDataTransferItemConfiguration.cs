using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class HouseholdDataTransferItemConfiguration : IEntityTypeConfiguration<HouseholdDataTransferItem>
{
    public void Configure(EntityTypeBuilder<HouseholdDataTransferItem> builder)
    {
        builder.ToTable("household_data_transfer_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.TransferId).HasColumnName("transfer_id").HasColumnType("uuid").IsRequired();

        builder.Property(i => i.Category).HasColumnName("category")
            .HasColumnType("character varying(128)").HasMaxLength(128).IsRequired();
        builder.Property(i => i.SourceId).HasColumnName("source_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.Label).HasColumnName("label")
            .HasColumnType("character varying(400)").HasMaxLength(400);

        builder.Property(i => i.Classification).HasColumnName("classification").HasConversion<string>()
            .HasColumnType("character varying(20)").HasMaxLength(20).IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<string>()
            .HasColumnType("character varying(20)").HasMaxLength(20).IsRequired();
        builder.Property(i => i.Decision).HasColumnName("decision").HasConversion<string>()
            .HasColumnType("character varying(20)").HasMaxLength(20);

        builder.Property(i => i.SourceUpdatedAt).HasColumnName("source_updated_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(i => i.TargetUpdatedAt).HasColumnName("target_updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(i => i.ErrorMessage).HasColumnName("error_message").HasColumnType("text");

        builder.Property(i => i.CreatedAt).HasColumnName("created_at")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.HasOne(i => i.Transfer)
            .WithMany(t => t.Items)
            .HasForeignKey(i => i.TransferId)
            .OnDelete(DeleteBehavior.Cascade);

        // One item per row of the archive, and the lookup the applier does per row.
        builder.HasIndex(i => new { i.TransferId, i.Category, i.SourceId }).IsUnique();

        // The report pages by classification within a transfer.
        builder.HasIndex(i => new { i.TransferId, i.Classification });
    }
}
