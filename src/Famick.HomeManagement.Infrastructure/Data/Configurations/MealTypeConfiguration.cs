using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class MealTypeConfiguration : IEntityTypeConfiguration<MealType>
{
    public void Configure(EntityTypeBuilder<MealType> builder)
    {
        builder.ToTable("meal_types");

        builder.HasKey(mt => mt.Id);

        builder.Property(mt => mt.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mt => mt.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mt => mt.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(mt => mt.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(mt => mt.IsDefault)
            .HasColumnName("is_default")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(mt => mt.Color)
            .HasColumnName("color")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(mt => mt.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(mt => mt.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(mt => mt.TenantId)
            .HasDatabaseName("ix_meal_types_tenant_id");

        // Unique constraint on (tenant_id, name) for case-insensitive uniqueness
        builder.HasIndex(mt => new { mt.TenantId, mt.Name })
            .IsUnique()
            .HasDatabaseName("ux_meal_types_tenant_name");
    }
}
