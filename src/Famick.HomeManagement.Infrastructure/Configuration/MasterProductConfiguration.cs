using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class MasterProductConfiguration : IEntityTypeConfiguration<MasterProduct>
{
    public void Configure(EntityTypeBuilder<MasterProduct> builder)
    {
        builder.ToTable("master_products");

        builder.HasKey(mp => mp.Id);

        builder.Property(mp => mp.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mp => mp.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(300)")
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(mp => mp.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(mp => mp.Category)
            .HasColumnName("category")
            .HasColumnType("character varying(100)")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(mp => mp.Brand)
            .HasColumnName("brand")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200);

        builder.Property(mp => mp.ContainerType)
            .HasColumnName("container_type")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(mp => mp.GramsPerTbsp)
            .HasColumnName("grams_per_tbsp")
            .HasColumnType("numeric(10,4)");

        builder.Property(mp => mp.IconSvg)
            .HasColumnName("icon_svg")
            .HasColumnType("text");

        builder.Property(mp => mp.ServingSize)
            .HasColumnName("serving_size")
            .HasColumnType("numeric(10,4)");

        builder.Property(mp => mp.ServingUnit)
            .HasColumnName("serving_unit")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(mp => mp.ServingsPerContainer)
            .HasColumnName("servings_per_container")
            .HasColumnType("numeric(10,4)");

        builder.Property(mp => mp.DefaultBestBeforeDays)
            .HasColumnName("default_best_before_days")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(mp => mp.TracksBestBeforeDate)
            .HasColumnName("tracks_best_before_date")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(mp => mp.IsStaple)
            .HasColumnName("is_staple")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(mp => mp.Popularity)
            .HasColumnName("popularity")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(mp => mp.LifestyleTags)
            .HasColumnName("lifestyle_tags")
            .HasColumnType("text")
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(mp => mp.AllergenFlags)
            .HasColumnName("allergen_flags")
            .HasColumnType("text")
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(mp => mp.DietaryConflictFlags)
            .HasColumnName("dietary_conflict_flags")
            .HasColumnType("text")
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(mp => mp.OrganicScore)
            .HasColumnName("organic_score")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(mp => mp.ConvenienceScore)
            .HasColumnName("convenience_score")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(mp => mp.HealthScore)
            .HasColumnName("health_score")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(mp => mp.DefaultLocationHint)
            .HasColumnName("default_location_hint")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(mp => mp.DefaultQuantityUnitHint)
            .HasColumnName("default_quantity_unit_hint")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(mp => mp.DataSourceAttribution)
            .HasColumnName("data_source_attribution")
            .HasColumnType("text");

        builder.Property(mp => mp.ParentMasterProductId)
            .HasColumnName("parent_master_product_id")
            .HasColumnType("uuid");

        builder.Property(mp => mp.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(mp => mp.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // Self-referencing parent-child hierarchy
        builder.HasOne(mp => mp.ParentMasterProduct)
            .WithMany(mp => mp.ChildMasterProducts)
            .HasForeignKey(mp => mp.ParentMasterProductId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique constraint: (Name, Category, Brand) — Brand null = generic
        builder.HasIndex(mp => new { mp.Name, mp.Category, mp.Brand })
            .IsUnique()
            .HasDatabaseName("ux_master_products_name_category_brand");
    }
}
