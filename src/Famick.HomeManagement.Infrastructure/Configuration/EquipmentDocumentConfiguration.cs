using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class EquipmentDocumentConfiguration : IEntityTypeConfiguration<EquipmentDocument>
{
    public void Configure(EntityTypeBuilder<EquipmentDocument> builder)
    {
        builder.ToTable("equipment_documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(d => d.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(d => d.EquipmentId)
            .HasColumnName("equipment_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(d => d.FileName)
            .HasColumnName("file_name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.ContentType)
            .HasColumnName("content_type")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.FileSize)
            .HasColumnName("file_size")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(d => d.DisplayName)
            .HasColumnName("display_name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(d => d.SortOrder)
            .HasColumnName("sort_order")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(d => d.TagId)
            .HasColumnName("tag_id")
            .HasColumnType("uuid");

        // Audit timestamps
        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(d => d.TenantId)
            .HasDatabaseName("ix_equipment_documents_tenant_id");

        builder.HasIndex(d => d.EquipmentId)
            .HasDatabaseName("ix_equipment_documents_equipment_id");

        builder.HasIndex(d => new { d.TenantId, d.EquipmentId })
            .HasDatabaseName("ix_equipment_documents_tenant_equipment");

        // Foreign keys
        builder.HasOne(d => d.Equipment)
            .WithMany(e => e.Documents)
            .HasForeignKey(d => d.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_equipment_documents_equipment");

        builder.HasOne(d => d.Tag)
            .WithMany(t => t.Documents)
            .HasForeignKey(d => d.TagId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_equipment_documents_tags");
    }
}
