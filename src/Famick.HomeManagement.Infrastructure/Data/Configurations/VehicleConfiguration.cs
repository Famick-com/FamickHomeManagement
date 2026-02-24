using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.TenantId)
            .IsRequired();

        builder.HasIndex(v => v.TenantId);

        builder.Property(v => v.Year)
            .IsRequired();

        builder.Property(v => v.Make)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(v => v.Model)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(v => v.Trim)
            .HasMaxLength(100);

        builder.Property(v => v.Vin)
            .HasMaxLength(17);

        builder.HasIndex(v => new { v.TenantId, v.Vin })
            .IsUnique()
            .HasFilter("\"Vin\" IS NOT NULL");

        builder.Property(v => v.LicensePlate)
            .HasMaxLength(20);

        builder.Property(v => v.Color)
            .HasMaxLength(50);

        builder.Property(v => v.PurchasePrice)
            .HasPrecision(18, 2);

        builder.Property(v => v.PurchaseLocation)
            .HasMaxLength(200);

        builder.Property(v => v.Notes)
            .HasMaxLength(2000);

        builder.Property(v => v.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(v => v.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation to primary driver
        builder.HasOne(v => v.PrimaryDriver)
            .WithMany()
            .HasForeignKey(v => v.PrimaryDriverContactId)
            .OnDelete(DeleteBehavior.SetNull);

        // Navigation to related collections
        builder.HasMany(v => v.MileageLogs)
            .WithOne(m => m.Vehicle)
            .HasForeignKey(m => m.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Documents)
            .WithOne(d => d.Vehicle)
            .HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.MaintenanceRecords)
            .WithOne(r => r.Vehicle)
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.MaintenanceSchedules)
            .WithOne(s => s.Vehicle)
            .HasForeignKey(s => s.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore computed properties
        builder.Ignore(v => v.DisplayName);
        builder.Ignore(v => v.FullName);
    }
}
