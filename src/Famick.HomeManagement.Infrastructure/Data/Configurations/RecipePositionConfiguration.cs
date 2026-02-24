using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class RecipePositionConfiguration : IEntityTypeConfiguration<RecipePosition>
{
    public void Configure(EntityTypeBuilder<RecipePosition> builder)
    {
        builder.ToTable("recipe_positions");

        builder.HasKey(rp => rp.Id);

        builder.Property(rp => rp.TenantId)
            .IsRequired();

        builder.HasIndex(rp => rp.TenantId);

        builder.HasIndex(rp => rp.RecipeStepId);

        builder.HasIndex(rp => rp.ProductId);

        builder.Property(rp => rp.Amount)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(rp => rp.AmountInGrams)
            .IsRequired()
            .HasPrecision(18, 4)
            .HasDefaultValue(0m);

        builder.Property(rp => rp.Note)
            .HasMaxLength(500);

        builder.Property(rp => rp.IngredientGroup)
            .HasMaxLength(100);

        builder.Property(rp => rp.OnlyCheckSingleUnitInStock)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(rp => rp.NotCheckStockFulfillment)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(rp => rp.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(rp => rp.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // FK: Product (restrict - don't cascade delete)
        builder.HasOne(rp => rp.Product)
            .WithMany()
            .HasForeignKey(rp => rp.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: QuantityUnit (restrict)
        builder.HasOne(rp => rp.QuantityUnit)
            .WithMany()
            .HasForeignKey(rp => rp.QuantityUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
