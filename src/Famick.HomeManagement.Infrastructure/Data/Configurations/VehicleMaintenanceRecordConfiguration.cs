using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class VehicleMaintenanceRecordConfiguration : IEntityTypeConfiguration<VehicleMaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<VehicleMaintenanceRecord> builder)
    {
        builder.ToTable("vehicle_maintenance_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.HasIndex(r => r.TenantId);

        builder.Property(r => r.VehicleId)
            .IsRequired();

        builder.HasIndex(r => r.VehicleId);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.CompletedDate)
            .IsRequired();

        builder.HasIndex(r => new { r.VehicleId, r.CompletedDate });

        builder.Property(r => r.Cost)
            .HasPrecision(18, 2);

        builder.Property(r => r.ServiceProvider)
            .HasMaxLength(200);

        builder.Property(r => r.Notes)
            .HasMaxLength(2000);

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation to maintenance schedule (optional)
        builder.HasOne(r => r.MaintenanceSchedule)
            .WithMany(s => s.MaintenanceRecords)
            .HasForeignKey(r => r.MaintenanceScheduleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
