using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class MealConfiguration : IEntityTypeConfiguration<Meal>
{
    public void Configure(EntityTypeBuilder<Meal> builder)
    {
        builder.ToTable("meals");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(m => m.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Notes)
            .HasColumnName("notes")
            .HasColumnType("character varying(2000)")
            .HasMaxLength(2000);

        builder.Property(m => m.IsFavorite)
            .HasColumnName("is_favorite")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(m => m.TenantId)
            .HasDatabaseName("ix_meals_tenant_id");

        builder.HasIndex(m => new { m.TenantId, m.Name })
            .HasDatabaseName("ix_meals_tenant_name");

        builder.HasIndex(m => new { m.TenantId, m.IsFavorite })
            .HasDatabaseName("ix_meals_tenant_favorite");
    }
}
