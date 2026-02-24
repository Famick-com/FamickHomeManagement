using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");

        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.TenantId)
            .IsRequired();

        builder.HasIndex(pi => pi.TenantId);

        builder.Property(pi => pi.ProductId)
            .IsRequired();

        builder.Property(pi => pi.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(pi => pi.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(pi => pi.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(pi => pi.FileSize)
            .IsRequired();

        builder.Property(pi => pi.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(pi => pi.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(pi => pi.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Index for efficient querying by product
        builder.HasIndex(pi => new { pi.TenantId, pi.ProductId });

        // Navigation
        builder.HasOne(pi => pi.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
