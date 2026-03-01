using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class ProductDietaryConflictConfiguration : IEntityTypeConfiguration<ProductDietaryConflict>
{
    public void Configure(EntityTypeBuilder<ProductDietaryConflict> builder)
    {
        builder.ToTable("product_dietary_conflicts");

        builder.HasKey(dc => dc.Id);

        builder.Property(dc => dc.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(dc => dc.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(dc => dc.DietaryPreference)
            .HasColumnName("dietary_preference")
            .IsRequired();

        builder.Property(dc => dc.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(dc => dc.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one dietary conflict per product
        builder.HasIndex(dc => new { dc.ProductId, dc.DietaryPreference })
            .IsUnique()
            .HasDatabaseName("ux_product_dietary_conflicts_product_pref");

        // Foreign keys
        builder.HasOne(dc => dc.Product)
            .WithMany(p => p.DietaryConflicts)
            .HasForeignKey(dc => dc.ProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_product_dietary_conflicts_product");
    }
}
