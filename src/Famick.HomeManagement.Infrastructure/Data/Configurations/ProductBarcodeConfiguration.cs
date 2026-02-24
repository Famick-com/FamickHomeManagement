using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class ProductBarcodeConfiguration : IEntityTypeConfiguration<ProductBarcode>
{
    public void Configure(EntityTypeBuilder<ProductBarcode> builder)
    {
        builder.ToTable("product_barcodes");

        builder.HasKey(pb => pb.Id);

        builder.Property(pb => pb.TenantId)
            .IsRequired();

        builder.HasIndex(pb => pb.TenantId);

        builder.Property(pb => pb.ProductId)
            .IsRequired();

        builder.Property(pb => pb.Barcode)
            .IsRequired()
            .HasMaxLength(200);

        // Unique constraint to prevent duplicate barcodes within a tenant
        builder.HasIndex(pb => new { pb.TenantId, pb.Barcode })
            .IsUnique();

        builder.Property(pb => pb.Note)
            .HasMaxLength(500);

        builder.Property(pb => pb.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation
        builder.HasOne(pb => pb.Product)
            .WithMany(p => p.Barcodes)
            .HasForeignKey(pb => pb.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
