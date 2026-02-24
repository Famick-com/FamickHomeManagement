using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class EquipmentUsageLogConfiguration : IEntityTypeConfiguration<EquipmentUsageLog>
{
    public void Configure(EntityTypeBuilder<EquipmentUsageLog> builder)
    {
        builder.ToTable("equipment_usage_logs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.EquipmentId)
            .HasColumnName("equipment_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.Date)
            .HasColumnName("date")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(l => l.Reading)
            .HasColumnName("reading")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(l => l.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        // Audit timestamps
        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(l => l.TenantId)
            .HasDatabaseName("ix_equipment_usage_logs_tenant_id");

        builder.HasIndex(l => l.EquipmentId)
            .HasDatabaseName("ix_equipment_usage_logs_equipment_id");

        builder.HasIndex(l => new { l.EquipmentId, l.Date })
            .HasDatabaseName("ix_equipment_usage_logs_equipment_date");

        // Foreign key to Equipment (cascade delete)
        builder.HasOne(l => l.Equipment)
            .WithMany(e => e.UsageLogs)
            .HasForeignKey(l => l.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_equipment_usage_logs_equipment");
    }
}
