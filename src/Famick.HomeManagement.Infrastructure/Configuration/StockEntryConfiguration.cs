using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration
{
    public class StockEntryConfiguration : IEntityTypeConfiguration<StockEntry>
    {
        public void Configure(EntityTypeBuilder<StockEntry> builder)
        {
            builder.ToTable("stock");

            builder.HasKey(s => s.Id);

            builder.Property(s => s.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(s => s.TenantId)
                .HasColumnName("tenant_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(s => s.ProductId)
                .HasColumnName("product_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(s => s.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            builder.Property(s => s.BestBeforeDate)
                .HasColumnName("best_before_date")
                .HasColumnType("timestamp with time zone");

            builder.Property(s => s.PurchasedDate)
                .HasColumnName("purchased_date")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(s => s.StockId)
                .HasColumnName("stock_id")
                .HasColumnType("character varying(100)")
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.Price)
                .HasColumnName("price")
                .HasColumnType("numeric(18,4)");

            builder.Property(s => s.Open)
                .HasColumnName("open")
                .HasColumnType("boolean")
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(s => s.OpenedDate)
                .HasColumnName("opened_date")
                .HasColumnType("timestamp with time zone");

            builder.Property(s => s.OpenTrackingMode)
                .HasColumnName("open_tracking_mode")
                .HasColumnType("integer");

            builder.Property(s => s.OriginalAmount)
                .HasColumnName("original_amount")
                .HasColumnType("numeric(18,4)");

            builder.Property(s => s.LocationId)
                .HasColumnName("location_id")
                .HasColumnType("uuid");

            builder.Property(s => s.ShoppingLocationId)
                .HasColumnName("shopping_location_id")
                .HasColumnType("uuid");

            builder.Property(s => s.Note)
                .HasColumnName("note")
                .HasColumnType("text");

            builder.Property(s => s.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(s => s.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(s => s.TenantId)
                .HasDatabaseName("ix_stock_tenant_id");

            builder.HasIndex(s => new { s.ProductId, s.Open, s.BestBeforeDate, s.Amount })
                .HasDatabaseName("ix_stock_performance1");

            builder.HasIndex(s => s.StockId)
                .HasDatabaseName("ix_stock_stock_id");

            // Foreign keys
            builder.HasOne(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_stock_products");

            builder.HasOne(s => s.Location)
                .WithMany()
                .HasForeignKey(s => s.LocationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_stock_locations");

            // Unique constraint on (TenantId, StockId) for multi-tenancy
            builder.HasIndex(s => new { s.TenantId, s.StockId })
                .IsUnique()
                .HasDatabaseName("ux_stock_tenant_stock_id");
        }
    }
}
