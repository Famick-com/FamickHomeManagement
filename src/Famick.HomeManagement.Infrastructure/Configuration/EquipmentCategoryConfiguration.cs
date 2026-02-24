using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class EquipmentCategoryConfiguration : IEntityTypeConfiguration<EquipmentCategory>
{
    public void Configure(EntityTypeBuilder<EquipmentCategory> builder)
    {
        builder.ToTable("equipment_categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(c => c.IconName)
            .HasColumnName("icon_name")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(c => c.SortOrder)
            .HasColumnName("sort_order")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(0);

        // Audit timestamps
        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("ix_equipment_categories_tenant_id");

        // Unique constraint: category names unique per tenant
        builder.HasIndex(c => new { c.TenantId, c.Name })
            .IsUnique()
            .HasDatabaseName("ux_equipment_categories_tenant_name");
    }
}
