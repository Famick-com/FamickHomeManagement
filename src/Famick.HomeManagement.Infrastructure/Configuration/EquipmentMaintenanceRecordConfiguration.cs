using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class EquipmentMaintenanceRecordConfiguration : IEntityTypeConfiguration<EquipmentMaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<EquipmentMaintenanceRecord> builder)
    {
        builder.ToTable("equipment_maintenance_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.EquipmentId)
            .HasColumnName("equipment_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.CompletedDate)
            .HasColumnName("completed_date")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(r => r.UsageAtCompletion)
            .HasColumnName("usage_at_completion")
            .HasColumnType("numeric(18,2)");

        builder.Property(r => r.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(r => r.ReminderChoreId)
            .HasColumnName("reminder_chore_id")
            .HasColumnType("uuid");

        // Audit timestamps
        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("ix_equipment_maintenance_records_tenant_id");

        builder.HasIndex(r => r.EquipmentId)
            .HasDatabaseName("ix_equipment_maintenance_records_equipment_id");

        builder.HasIndex(r => new { r.EquipmentId, r.CompletedDate })
            .HasDatabaseName("ix_equipment_maintenance_records_equipment_date");

        // Foreign key to Equipment (cascade delete)
        builder.HasOne(r => r.Equipment)
            .WithMany(e => e.MaintenanceRecords)
            .HasForeignKey(r => r.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_equipment_maintenance_records_equipment");

        // Foreign key to Chore (set null on delete)
        builder.HasOne(r => r.ReminderChore)
            .WithMany()
            .HasForeignKey(r => r.ReminderChoreId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_equipment_maintenance_records_chore");
    }
}
