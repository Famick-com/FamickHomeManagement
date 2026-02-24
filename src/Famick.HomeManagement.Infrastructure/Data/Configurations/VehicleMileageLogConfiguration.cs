using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class VehicleMileageLogConfiguration : IEntityTypeConfiguration<VehicleMileageLog>
{
    public void Configure(EntityTypeBuilder<VehicleMileageLog> builder)
    {
        builder.ToTable("vehicle_mileage_logs");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId)
            .IsRequired();

        builder.HasIndex(m => m.TenantId);

        builder.Property(m => m.VehicleId)
            .IsRequired();

        builder.HasIndex(m => m.VehicleId);

        builder.Property(m => m.Mileage)
            .IsRequired();

        builder.Property(m => m.ReadingDate)
            .IsRequired();

        builder.HasIndex(m => new { m.VehicleId, m.ReadingDate });

        builder.Property(m => m.Notes)
            .HasMaxLength(500);

        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation configured in VehicleConfiguration
    }
}
