using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("equipment");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Icon)
            .HasColumnName("icon")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(e => e.Location)
            .HasColumnName("location")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(e => e.ModelNumber)
            .HasColumnName("model_number")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(e => e.SerialNumber)
            .HasColumnName("serial_number")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(e => e.Manufacturer)
            .HasColumnName("manufacturer")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(e => e.ManufacturerLink)
            .HasColumnName("manufacturer_link")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(e => e.UsageUnit)
            .HasColumnName("usage_unit")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(e => e.PurchaseDate)
            .HasColumnName("purchase_date")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.PurchaseLocation)
            .HasColumnName("purchase_location")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(e => e.WarrantyExpirationDate)
            .HasColumnName("warranty_expiration_date")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.WarrantyContactInfo)
            .HasColumnName("warranty_contact_info")
            .HasColumnType("text");

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(e => e.CategoryId)
            .HasColumnName("category_id")
            .HasColumnType("uuid");

        builder.Property(e => e.ParentEquipmentId)
            .HasColumnName("parent_equipment_id")
            .HasColumnType("uuid");

        // Audit timestamps
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_equipment_tenant_id");

        builder.HasIndex(e => e.CategoryId)
            .HasDatabaseName("ix_equipment_category_id");

        builder.HasIndex(e => e.ParentEquipmentId)
            .HasDatabaseName("ix_equipment_parent_id");

        builder.HasIndex(e => new { e.TenantId, e.Name })
            .HasDatabaseName("ix_equipment_tenant_name");

        builder.HasIndex(e => new { e.TenantId, e.Manufacturer })
            .HasDatabaseName("ix_equipment_tenant_manufacturer");

        // Self-referential FK for parent-child hierarchy
        builder.HasOne(e => e.ParentEquipment)
            .WithMany(e => e.ChildEquipment)
            .HasForeignKey(e => e.ParentEquipmentId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_equipment_parent");

        // Category FK
        builder.HasOne(e => e.Category)
            .WithMany(c => c.Equipment)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_equipment_category");

        // Documents (configured from EquipmentDocument side)
        // Chores (configured from Chore side)
    }
}
