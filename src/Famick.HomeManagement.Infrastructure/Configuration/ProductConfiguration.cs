using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Brand)
            .HasColumnName("brand")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200);

        builder.Property(p => p.MasterProductId)
            .HasColumnName("master_product_id")
            .HasColumnType("uuid");

        builder.Property(p => p.OverriddenFields)
            .HasColumnName("overridden_fields")
            .HasColumnType("text");

        builder.HasOne(p => p.MasterProduct)
            .WithMany()
            .HasForeignKey(p => p.MasterProductId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.MasterProductId)
            .HasDatabaseName("ix_products_master_product_id");
    }
}
