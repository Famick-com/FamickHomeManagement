using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class EquipmentDocumentTagConfiguration : IEntityTypeConfiguration<EquipmentDocumentTag>
{
    public void Configure(EntityTypeBuilder<EquipmentDocumentTag> builder)
    {
        builder.ToTable("equipment_document_tags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.IsDefault)
            .HasColumnName("is_default")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.SortOrder)
            .HasColumnName("sort_order")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(0);

        // Audit timestamps
        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(t => t.TenantId)
            .HasDatabaseName("ix_equipment_document_tags_tenant_id");

        // Unique constraint: tag names unique per tenant
        builder.HasIndex(t => new { t.TenantId, t.Name })
            .IsUnique()
            .HasDatabaseName("ux_equipment_document_tags_tenant_name");
    }
}
