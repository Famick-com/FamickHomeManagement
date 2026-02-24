using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ProductStoreMetadataConfiguration : IEntityTypeConfiguration<ProductStoreMetadata>
{
    public void Configure(EntityTypeBuilder<ProductStoreMetadata> builder)
    {
        builder.ToTable("product_store_metadata");

        builder.HasKey(psm => psm.Id);

        builder.Property(psm => psm.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(psm => psm.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(psm => psm.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(psm => psm.ShoppingLocationId)
            .HasColumnName("shopping_location_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(psm => psm.ExternalProductId)
            .HasColumnName("external_product_id")
            .HasColumnType("character varying(100)");

        builder.Property(psm => psm.LastKnownPrice)
            .HasColumnName("last_known_price")
            .HasColumnType("numeric(10,2)");

        builder.Property(psm => psm.PriceUnit)
            .HasColumnName("price_unit")
            .HasColumnType("character varying(50)");

        builder.Property(psm => psm.PriceUpdatedAt)
            .HasColumnName("price_updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(psm => psm.Aisle)
            .HasColumnName("aisle")
            .HasColumnType("character varying(50)");

        builder.Property(psm => psm.Shelf)
            .HasColumnName("shelf")
            .HasColumnType("character varying(50)");

        builder.Property(psm => psm.Department)
            .HasColumnName("department")
            .HasColumnType("character varying(100)");

        builder.Property(psm => psm.InStock)
            .HasColumnName("in_stock")
            .HasColumnType("boolean");

        builder.Property(psm => psm.AvailabilityCheckedAt)
            .HasColumnName("availability_checked_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(psm => psm.ProductUrl)
            .HasColumnName("product_url")
            .HasColumnType("text");

        builder.Property(psm => psm.CacheExpiresAt)
            .HasColumnName("cache_expires_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(psm => psm.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(psm => psm.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(psm => psm.TenantId)
            .HasDatabaseName("ix_product_store_metadata_tenant_id");

        // Unique constraint: one metadata entry per product per store per tenant
        builder.HasIndex(psm => new { psm.TenantId, psm.ProductId, psm.ShoppingLocationId })
            .IsUnique()
            .HasDatabaseName("ux_product_store_metadata_product_location");

        // Index for looking up all metadata for a product
        builder.HasIndex(psm => new { psm.TenantId, psm.ProductId })
            .HasDatabaseName("ix_product_store_metadata_tenant_product");

        // Index for looking up all metadata for a store
        builder.HasIndex(psm => new { psm.TenantId, psm.ShoppingLocationId })
            .HasDatabaseName("ix_product_store_metadata_tenant_location");

        // Navigation properties
        builder.HasOne(psm => psm.Product)
            .WithMany()
            .HasForeignKey(psm => psm.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // ShoppingLocation relationship is configured in ShoppingLocationConfiguration
    }
}
