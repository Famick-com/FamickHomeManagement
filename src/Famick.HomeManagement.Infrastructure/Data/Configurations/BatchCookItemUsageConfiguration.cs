using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class BatchCookItemUsageConfiguration : IEntityTypeConfiguration<BatchCookItemUsage>
{
    public void Configure(EntityTypeBuilder<BatchCookItemUsage> builder)
    {
        builder.ToTable("batch_cook_item_usages");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.BatchCookItemId)
            .HasColumnName("batch_cook_item_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.DependentEntryId)
            .HasColumnName("dependent_entry_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.QuantityUsed)
            .HasColumnName("quantity_used")
            .HasColumnType("numeric");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one usage per batch item per dependent entry
        builder.HasIndex(e => new { e.BatchCookItemId, e.DependentEntryId })
            .IsUnique()
            .HasDatabaseName("ux_batch_cook_item_usages_item_entry");

        // Index for querying by dependent entry
        builder.HasIndex(e => e.DependentEntryId)
            .HasDatabaseName("ix_batch_cook_item_usages_dependent_entry");

        // Foreign keys
        builder.HasOne(e => e.BatchCookItem)
            .WithMany(bci => bci.Usages)
            .HasForeignKey(e => e.BatchCookItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_batch_cook_item_usages_batch_cook_item");

        builder.HasOne(e => e.DependentEntry)
            .WithMany(entry => entry.BatchCookItemUsages)
            .HasForeignKey(e => e.DependentEntryId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_batch_cook_item_usages_dependent_entry");
    }
}
