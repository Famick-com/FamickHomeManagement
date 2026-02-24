using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration
{
    public class ProductNutritionConfiguration : IEntityTypeConfiguration<ProductNutrition>
    {
        public void Configure(EntityTypeBuilder<ProductNutrition> builder)
        {
            builder.ToTable("product_nutrition");

            builder.HasKey(pn => pn.Id);

            builder.Property(pn => pn.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(pn => pn.TenantId)
                .HasColumnName("tenant_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(pn => pn.ProductId)
                .HasColumnName("product_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(pn => pn.ExternalId)
                .HasColumnName("external_id")
                .HasColumnType("character varying(100)")
                .HasMaxLength(100);

            builder.Property(pn => pn.DataSource)
                .HasColumnName("data_source")
                .HasColumnType("character varying(50)")
                .IsRequired()
                .HasMaxLength(50);

            // Serving information
            builder.Property(pn => pn.ServingSize)
                .HasColumnName("serving_size")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.ServingUnit)
                .HasColumnName("serving_unit")
                .HasColumnType("character varying(50)")
                .HasMaxLength(50);

            builder.Property(pn => pn.ServingSizeDescription)
                .HasColumnName("serving_size_description")
                .HasColumnType("character varying(255)")
                .HasMaxLength(255);

            // Macronutrients
            builder.Property(pn => pn.Calories)
                .HasColumnName("calories")
                .HasColumnType("numeric(10,2)");

            builder.Property(pn => pn.TotalFat)
                .HasColumnName("total_fat")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.SaturatedFat)
                .HasColumnName("saturated_fat")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.TransFat)
                .HasColumnName("trans_fat")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Cholesterol)
                .HasColumnName("cholesterol")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Sodium)
                .HasColumnName("sodium")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.TotalCarbohydrates)
                .HasColumnName("total_carbohydrates")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.DietaryFiber)
                .HasColumnName("dietary_fiber")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.TotalSugars)
                .HasColumnName("total_sugars")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.AddedSugars)
                .HasColumnName("added_sugars")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Protein)
                .HasColumnName("protein")
                .HasColumnType("numeric(10,3)");

            // Vitamins
            builder.Property(pn => pn.VitaminA)
                .HasColumnName("vitamin_a")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.VitaminC)
                .HasColumnName("vitamin_c")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.VitaminD)
                .HasColumnName("vitamin_d")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.VitaminE)
                .HasColumnName("vitamin_e")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.VitaminK)
                .HasColumnName("vitamin_k")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Thiamin)
                .HasColumnName("thiamin")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Riboflavin)
                .HasColumnName("riboflavin")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Niacin)
                .HasColumnName("niacin")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.VitaminB6)
                .HasColumnName("vitamin_b6")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Folate)
                .HasColumnName("folate")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.VitaminB12)
                .HasColumnName("vitamin_b12")
                .HasColumnType("numeric(10,3)");

            // Minerals
            builder.Property(pn => pn.Calcium)
                .HasColumnName("calcium")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Iron)
                .HasColumnName("iron")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Magnesium)
                .HasColumnName("magnesium")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Phosphorus)
                .HasColumnName("phosphorus")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Potassium)
                .HasColumnName("potassium")
                .HasColumnType("numeric(10,3)");

            builder.Property(pn => pn.Zinc)
                .HasColumnName("zinc")
                .HasColumnType("numeric(10,3)");

            // Metadata
            builder.Property(pn => pn.BrandOwner)
                .HasColumnName("brand_owner")
                .HasColumnType("character varying(255)")
                .HasMaxLength(255);

            builder.Property(pn => pn.BrandName)
                .HasColumnName("brand_name")
                .HasColumnType("character varying(255)")
                .HasMaxLength(255);

            builder.Property(pn => pn.Ingredients)
                .HasColumnName("ingredients")
                .HasColumnType("text");

            builder.Property(pn => pn.LastUpdatedFromSource)
                .HasColumnName("last_updated_from_source")
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            builder.Property(pn => pn.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(pn => pn.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(pn => pn.TenantId)
                .HasDatabaseName("ix_product_nutrition_tenant_id");

            builder.HasIndex(pn => pn.ProductId)
                .IsUnique()
                .HasDatabaseName("ux_product_nutrition_product_id");

            builder.HasIndex(pn => new { pn.DataSource, pn.ExternalId })
                .HasDatabaseName("ix_product_nutrition_data_source_external_id");

            // One-to-one relationship with Product
            builder.HasOne(pn => pn.Product)
                .WithOne(p => p.Nutrition)
                .HasForeignKey<ProductNutrition>(pn => pn.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
