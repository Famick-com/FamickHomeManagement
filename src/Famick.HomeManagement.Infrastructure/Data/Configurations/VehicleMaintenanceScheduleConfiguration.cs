using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class VehicleMaintenanceScheduleConfiguration : IEntityTypeConfiguration<VehicleMaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<VehicleMaintenanceSchedule> builder)
    {
        builder.ToTable("vehicle_maintenance_schedules");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.HasIndex(s => s.TenantId);

        builder.Property(s => s.VehicleId)
            .IsRequired();

        builder.HasIndex(s => s.VehicleId);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(s => new { s.VehicleId, s.Name })
            .IsUnique();

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.Notes)
            .HasMaxLength(2000);

        builder.Property(s => s.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Index for finding upcoming maintenance
        builder.HasIndex(s => new { s.VehicleId, s.NextDueDate })
            .HasFilter("\"IsActive\" = true");

        builder.HasIndex(s => new { s.VehicleId, s.NextDueMileage })
            .HasFilter("\"IsActive\" = true");

        // Navigation to maintenance records configured in VehicleMaintenanceRecordConfiguration
    }
}
