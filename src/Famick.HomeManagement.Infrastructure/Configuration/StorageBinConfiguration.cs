using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class StorageBinConfiguration : IEntityTypeConfiguration<StorageBin>
{
    public void Configure(EntityTypeBuilder<StorageBin> builder)
    {
        builder.ToTable("storage_bins");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.ShortCode)
            .HasColumnName("short_code")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired()
            .HasDefaultValue(string.Empty);

        // Audit timestamps
        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Location relationship (optional)
        builder.Property(b => b.LocationId)
            .HasColumnName("location_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(b => b.Category)
            .HasColumnName("category")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired(false);

        builder.HasOne(b => b.Location)
            .WithMany(l => l.StorageBins)
            .HasForeignKey(b => b.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(b => b.TenantId)
            .HasDatabaseName("ix_storage_bins_tenant_id");

        builder.HasIndex(b => new { b.TenantId, b.ShortCode })
            .IsUnique()
            .HasDatabaseName("ix_storage_bins_tenant_short_code");

        builder.HasIndex(b => new { b.TenantId, b.LocationId })
            .HasDatabaseName("ix_storage_bins_tenant_location");

        builder.HasIndex(b => new { b.TenantId, b.Category })
            .HasDatabaseName("ix_storage_bins_tenant_category");

        // Photos relationship configured from StorageBinPhoto side
    }
}
