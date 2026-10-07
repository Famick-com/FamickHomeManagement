using System.Text.Json;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
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

        builder.Property(e => e.Kind)
            .HasColumnName("kind")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(EquipmentKind.Other);

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.PrimaryDriverContactId)
            .HasColumnName("primary_driver_contact_id")
            .HasColumnType("uuid");

        // Kind-specific fields as JSON. Converted explicitly rather than relying on Npgsql's
        // dynamic POCO-to-jsonb mapping, which is not enabled on this context.
        // An all-null attribute set is stored as SQL NULL so the partial VIN index stays small.
        builder.Property(e => e.Attributes)
            .HasColumnName("attributes")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null || v.IsEmpty ? null : JsonSerializer.Serialize(v, AttributesJsonOptions),
                v => string.IsNullOrWhiteSpace(v)
                    ? null
                    : JsonSerializer.Deserialize<EquipmentAttributes>(v, AttributesJsonOptions));

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

        builder.HasIndex(e => new { e.TenantId, e.Kind })
            .HasDatabaseName("ix_equipment_tenant_kind");

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

        // Primary driver FK. SetNull matters: deleting a contact must not delete the vehicle,
        // and must not leave it pointing at a contact that no longer exists. This is the reason
        // the driver is a real column rather than part of the JSON attributes.
        builder.HasOne(e => e.PrimaryDriver)
            .WithMany()
            .HasForeignKey(e => e.PrimaryDriverContactId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_equipment_primary_driver");

        // Documents (configured from EquipmentDocument side)
        // Chores (configured from Chore side)

        // Unique VIN per tenant, preserved from the vehicles table this folded into. EF cannot
        // model an index over a JSON expression, so it is created as raw SQL in the migration:
        //   CREATE UNIQUE INDEX ux_equipment_tenant_vin ON equipment
        //     (tenant_id, ((attributes ->> 'Vin'))) WHERE (attributes ->> 'Vin') IS NOT NULL;
        // Keep that index in step with EquipmentAttributes.Vin.
    }

    /// <summary>
    /// Omits nulls so an attribute set carries only the fields a kind actually uses, which keeps
    /// the stored JSON readable and stops a vehicle's keys appearing on an appliance.
    /// </summary>
    private static readonly JsonSerializerOptions AttributesJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
