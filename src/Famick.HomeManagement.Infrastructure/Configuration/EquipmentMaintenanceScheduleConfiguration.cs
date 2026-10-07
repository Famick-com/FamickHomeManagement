using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

/// <summary>
/// Maps <see cref="EquipmentMaintenanceSchedule"/>.
/// </summary>
/// <remarks>
/// Column names are snake_case to match the other <c>equipment_*</c> tables. The vehicle tables
/// this entity was ported from use PascalCase columns, because their configurations never set
/// column names and took EF's default — do not copy that side's style here.
/// </remarks>
public class EquipmentMaintenanceScheduleConfiguration : IEntityTypeConfiguration<EquipmentMaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<EquipmentMaintenanceSchedule> builder)
    {
        builder.ToTable("equipment_maintenance_schedules");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.EquipmentId)
            .HasColumnName("equipment_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasColumnType("character varying(1000)")
            .HasMaxLength(1000);

        builder.Property(s => s.IntervalMonths)
            .HasColumnName("interval_months")
            .HasColumnType("integer");

        builder.Property(s => s.IntervalUsage)
            .HasColumnName("interval_usage")
            .HasColumnType("numeric(18,2)");

        builder.Property(s => s.LastCompletedDate)
            .HasColumnName("last_completed_date")
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.LastCompletedUsage)
            .HasColumnName("last_completed_usage")
            .HasColumnType("numeric(18,2)");

        builder.Property(s => s.NextDueDate)
            .HasColumnName("next_due_date")
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.NextDueUsage)
            .HasColumnName("next_due_usage")
            .HasColumnType("numeric(18,2)");

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        // Audit timestamps
        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_equipment_maintenance_schedules_tenant_id");

        builder.HasIndex(s => s.EquipmentId)
            .HasDatabaseName("ix_equipment_maintenance_schedules_equipment_id");

        builder.HasIndex(s => new { s.EquipmentId, s.Name })
            .IsUnique()
            .HasDatabaseName("ux_equipment_maintenance_schedules_equipment_name");

        // These two exist to make the reminder queries cheap: both filter on IsActive and then
        // order/compare on a next-due value. Keep the filter in step with UpcomingReminderService.
        builder.HasIndex(s => new { s.EquipmentId, s.NextDueDate })
            .HasFilter("is_active = true")
            .HasDatabaseName("ix_equipment_maintenance_schedules_equipment_next_due_date");

        builder.HasIndex(s => new { s.EquipmentId, s.NextDueUsage })
            .HasFilter("is_active = true")
            .HasDatabaseName("ix_equipment_maintenance_schedules_equipment_next_due_usage");

        // Foreign key to Equipment (cascade delete)
        builder.HasOne(s => s.Equipment)
            .WithMany(e => e.MaintenanceSchedules)
            .HasForeignKey(s => s.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_equipment_maintenance_schedules_equipment");
    }
}
