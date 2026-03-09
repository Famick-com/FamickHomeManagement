using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class MasterProductNutritionConfiguration : IEntityTypeConfiguration<MasterProductNutrition>
{
    public void Configure(EntityTypeBuilder<MasterProductNutrition> builder)
    {
        builder.ToTable("master_product_nutrition");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id").HasColumnType("uuid").IsRequired();
        builder.Property(n => n.MasterProductId).HasColumnName("master_product_id").HasColumnType("uuid").IsRequired();
        builder.Property(n => n.ExternalId).HasColumnName("external_id").HasColumnType("character varying(100)").HasMaxLength(100);
        builder.Property(n => n.DataSource).HasColumnName("data_source").HasColumnType("character varying(50)").IsRequired().HasMaxLength(50);

        // Serving information
        builder.Property(n => n.ServingSize).HasColumnName("serving_size").HasColumnType("numeric(10,4)");
        builder.Property(n => n.ServingUnit).HasColumnName("serving_unit").HasColumnType("character varying(50)").HasMaxLength(50);
        builder.Property(n => n.ServingsPerContainer).HasColumnName("servings_per_container").HasColumnType("numeric(10,4)");

        // Macronutrients
        builder.Property(n => n.Calories).HasColumnName("calories").HasColumnType("numeric(10,4)");
        builder.Property(n => n.TotalFat).HasColumnName("total_fat").HasColumnType("numeric(10,4)");
        builder.Property(n => n.SaturatedFat).HasColumnName("saturated_fat").HasColumnType("numeric(10,4)");
        builder.Property(n => n.TransFat).HasColumnName("trans_fat").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Cholesterol).HasColumnName("cholesterol").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Sodium).HasColumnName("sodium").HasColumnType("numeric(10,4)");
        builder.Property(n => n.TotalCarbohydrates).HasColumnName("total_carbohydrates").HasColumnType("numeric(10,4)");
        builder.Property(n => n.DietaryFiber).HasColumnName("dietary_fiber").HasColumnType("numeric(10,4)");
        builder.Property(n => n.TotalSugars).HasColumnName("total_sugars").HasColumnType("numeric(10,4)");
        builder.Property(n => n.AddedSugars).HasColumnName("added_sugars").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Protein).HasColumnName("protein").HasColumnType("numeric(10,4)");

        // Vitamins and Minerals
        builder.Property(n => n.VitaminA).HasColumnName("vitamin_a").HasColumnType("numeric(10,4)");
        builder.Property(n => n.VitaminC).HasColumnName("vitamin_c").HasColumnType("numeric(10,4)");
        builder.Property(n => n.VitaminD).HasColumnName("vitamin_d").HasColumnType("numeric(10,4)");
        builder.Property(n => n.VitaminE).HasColumnName("vitamin_e").HasColumnType("numeric(10,4)");
        builder.Property(n => n.VitaminK).HasColumnName("vitamin_k").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Thiamin).HasColumnName("thiamin").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Riboflavin).HasColumnName("riboflavin").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Niacin).HasColumnName("niacin").HasColumnType("numeric(10,4)");
        builder.Property(n => n.VitaminB6).HasColumnName("vitamin_b6").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Folate).HasColumnName("folate").HasColumnType("numeric(10,4)");
        builder.Property(n => n.VitaminB12).HasColumnName("vitamin_b12").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Calcium).HasColumnName("calcium").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Iron).HasColumnName("iron").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Magnesium).HasColumnName("magnesium").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Phosphorus).HasColumnName("phosphorus").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Potassium).HasColumnName("potassium").HasColumnType("numeric(10,4)");
        builder.Property(n => n.Zinc).HasColumnName("zinc").HasColumnType("numeric(10,4)");

        // Metadata
        builder.Property(n => n.BrandOwner).HasColumnName("brand_owner").HasColumnType("character varying(200)").HasMaxLength(200);
        builder.Property(n => n.BrandName).HasColumnName("brand_name").HasColumnType("character varying(200)").HasMaxLength(200);
        builder.Property(n => n.Ingredients).HasColumnName("ingredients").HasColumnType("text");
        builder.Property(n => n.ServingSizeDescription).HasColumnName("serving_size_description").HasColumnType("character varying(200)").HasMaxLength(200);
        builder.Property(n => n.LastUpdatedFromSource).HasColumnName("last_updated_from_source").HasColumnType("timestamp with time zone").IsRequired();

        builder.Property(n => n.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

        // One-to-one with MasterProduct
        builder.HasOne(n => n.MasterProduct)
            .WithOne(mp => mp.Nutrition)
            .HasForeignKey<MasterProductNutrition>(n => n.MasterProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => n.MasterProductId)
            .IsUnique()
            .HasDatabaseName("ux_master_product_nutrition_master_product_id");
    }
}
