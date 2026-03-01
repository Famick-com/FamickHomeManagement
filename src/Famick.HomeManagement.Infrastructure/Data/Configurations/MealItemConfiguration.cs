using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class MealItemConfiguration : IEntityTypeConfiguration<MealItem>
{
    public void Configure(EntityTypeBuilder<MealItem> builder)
    {
        builder.ToTable("meal_items");

        builder.HasKey(mi => mi.Id);

        builder.Property(mi => mi.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mi => mi.MealId)
            .HasColumnName("meal_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mi => mi.ItemType)
            .HasColumnName("item_type")
            .IsRequired();

        builder.Property(mi => mi.RecipeId)
            .HasColumnName("recipe_id")
            .HasColumnType("uuid");

        builder.Property(mi => mi.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid");

        builder.Property(mi => mi.ProductQuantity)
            .HasColumnName("product_quantity")
            .HasPrecision(18, 4);

        builder.Property(mi => mi.ProductQuantityUnitId)
            .HasColumnName("product_quantity_unit_id")
            .HasColumnType("uuid");

        builder.Property(mi => mi.FreetextDescription)
            .HasColumnName("freetext_description")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(mi => mi.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(mi => mi.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(mi => mi.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(mi => mi.MealId)
            .HasDatabaseName("ix_meal_items_meal_id");

        // Foreign keys
        builder.HasOne(mi => mi.Meal)
            .WithMany(m => m.Items)
            .HasForeignKey(mi => mi.MealId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_meal_items_meal");

        builder.HasOne(mi => mi.Recipe)
            .WithMany()
            .HasForeignKey(mi => mi.RecipeId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_meal_items_recipe");

        builder.HasOne(mi => mi.Product)
            .WithMany()
            .HasForeignKey(mi => mi.ProductId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_meal_items_product");

        builder.HasOne(mi => mi.ProductQuantityUnit)
            .WithMany()
            .HasForeignKey(mi => mi.ProductQuantityUnitId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_meal_items_quantity_unit");
    }
}
