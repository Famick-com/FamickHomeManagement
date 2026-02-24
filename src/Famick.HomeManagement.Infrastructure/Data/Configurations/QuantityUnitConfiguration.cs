using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class QuantityUnitConfiguration : IEntityTypeConfiguration<QuantityUnit>
{
    public void Configure(EntityTypeBuilder<QuantityUnit> builder)
    {
        builder.ToTable("quantity_units");

        builder.HasKey(qu => qu.Id);

        builder.Property(qu => qu.TenantId)
            .IsRequired();

        builder.HasIndex(qu => qu.TenantId);

        builder.Property(qu => qu.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(qu => new { qu.TenantId, qu.Name })
            .IsUnique();

        builder.Property(qu => qu.NamePlural)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(qu => qu.Description)
            .HasMaxLength(1000);

        builder.Property(qu => qu.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(qu => qu.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation - configure bidirectional relationship with Products
        builder.HasMany(qu => qu.ProductsWithPurchaseUnit)
            .WithOne(p => p.QuantityUnitPurchase)
            .HasForeignKey(p => p.QuantityUnitIdPurchase)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(qu => qu.ProductsWithStockUnit)
            .WithOne(p => p.QuantityUnitStock)
            .HasForeignKey(p => p.QuantityUnitIdStock)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
