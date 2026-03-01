using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class ProductAllergenConfiguration : IEntityTypeConfiguration<ProductAllergen>
{
    public void Configure(EntityTypeBuilder<ProductAllergen> builder)
    {
        builder.ToTable("product_allergens");

        builder.HasKey(pa => pa.Id);

        builder.Property(pa => pa.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(pa => pa.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(pa => pa.AllergenType)
            .HasColumnName("allergen_type")
            .IsRequired();

        builder.Property(pa => pa.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(pa => pa.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one allergen type per product
        builder.HasIndex(pa => new { pa.ProductId, pa.AllergenType })
            .IsUnique()
            .HasDatabaseName("ux_product_allergens_product_type");

        // Foreign keys
        builder.HasOne(pa => pa.Product)
            .WithMany(p => p.Allergens)
            .HasForeignKey(pa => pa.ProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_product_allergens_product");
    }
}
