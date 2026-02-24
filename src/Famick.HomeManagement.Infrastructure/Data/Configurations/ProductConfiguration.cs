using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.HasIndex(p => p.TenantId);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.HasIndex(p => new { p.TenantId, p.Name });

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.LocationId)
            .IsRequired();

        builder.Property(p => p.QuantityUnitIdPurchase)
            .IsRequired();

        builder.Property(p => p.QuantityUnitIdStock)
            .IsRequired();

        builder.Property(p => p.QuantityUnitFactorPurchaseToStock)
            .IsRequired()
            .HasPrecision(18, 4)
            .HasDefaultValue(1.0m);

        builder.Property(p => p.MinStockAmount)
            .IsRequired()
            .HasPrecision(18, 4)
            .HasDefaultValue(0m);

        builder.Property(p => p.DefaultBestBeforeDays)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.TracksBestBeforeDate)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.ExpiryWarningDays);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation
        builder.HasOne(p => p.Location)
            .WithMany(l => l.Products)
            .HasForeignKey(p => p.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.QuantityUnitPurchase)
            .WithMany(qu => qu.ProductsWithPurchaseUnit)
            .HasForeignKey(p => p.QuantityUnitIdPurchase)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.QuantityUnitStock)
            .WithMany(qu => qu.ProductsWithStockUnit)
            .HasForeignKey(p => p.QuantityUnitIdStock)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Barcodes)
            .WithOne(pb => pb.Product)
            .HasForeignKey(pb => pb.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Store-specific metadata (price, aisle, availability per store)
        builder.HasMany(p => p.StoreMetadata)
            .WithOne(m => m.Product)
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.DataSourceAttribution);

        // Self-referential FK for parent-child hierarchy (product variants)
        builder.HasIndex(p => p.ParentProductId)
            .HasDatabaseName("ix_products_parent_product_id");

        builder.HasOne(p => p.ParentProduct)
            .WithMany(p => p.ChildProducts)
            .HasForeignKey(p => p.ParentProductId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_products_parent_product");
    }
}
