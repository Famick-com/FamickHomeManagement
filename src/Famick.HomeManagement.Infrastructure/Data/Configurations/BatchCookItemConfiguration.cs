using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class BatchCookItemConfiguration : IEntityTypeConfiguration<BatchCookItem>
{
    public void Configure(EntityTypeBuilder<BatchCookItem> builder)
    {
        builder.ToTable("batch_cook_items");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.SourceEntryId)
            .HasColumnName("source_entry_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.TotalQuantity)
            .HasColumnName("total_quantity")
            .HasColumnType("numeric");

        builder.Property(e => e.QuantityUnitId)
            .HasColumnName("quantity_unit_id")
            .HasColumnType("uuid");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one product per source entry
        builder.HasIndex(e => new { e.SourceEntryId, e.ProductId })
            .IsUnique()
            .HasDatabaseName("ux_batch_cook_items_source_product");

        // Foreign keys
        builder.HasOne(e => e.SourceEntry)
            .WithMany(entry => entry.BatchCookItems)
            .HasForeignKey(e => e.SourceEntryId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_batch_cook_items_source_entry");

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_batch_cook_items_product");

        builder.HasOne(e => e.QuantityUnit)
            .WithMany()
            .HasForeignKey(e => e.QuantityUnitId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_batch_cook_items_quantity_unit");
    }
}
