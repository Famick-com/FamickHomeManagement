using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class StorageBinPhotoConfiguration : IEntityTypeConfiguration<StorageBinPhoto>
{
    public void Configure(EntityTypeBuilder<StorageBinPhoto> builder)
    {
        builder.ToTable("storage_bin_photos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.StorageBinId)
            .HasColumnName("storage_bin_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.FileName)
            .HasColumnName("file_name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.ContentType)
            .HasColumnName("content_type")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.FileSize)
            .HasColumnName("file_size")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(p => p.SortOrder)
            .HasColumnName("sort_order")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(0);

        // Audit timestamps
        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("ix_storage_bin_photos_tenant_id");

        builder.HasIndex(p => p.StorageBinId)
            .HasDatabaseName("ix_storage_bin_photos_storage_bin_id");

        builder.HasIndex(p => new { p.TenantId, p.StorageBinId })
            .HasDatabaseName("ix_storage_bin_photos_tenant_storage_bin");

        // Foreign key to StorageBin with cascade delete
        builder.HasOne(p => p.StorageBin)
            .WithMany(b => b.Photos)
            .HasForeignKey(p => p.StorageBinId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_storage_bin_photos_storage_bin");
    }
}
