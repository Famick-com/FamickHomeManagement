using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    public void Configure(EntityTypeBuilder<VehicleDocument> builder)
    {
        builder.ToTable("vehicle_documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId)
            .IsRequired();

        builder.HasIndex(d => d.TenantId);

        builder.Property(d => d.VehicleId)
            .IsRequired();

        builder.HasIndex(d => d.VehicleId);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.FileSize)
            .IsRequired();

        builder.Property(d => d.DisplayName)
            .HasMaxLength(200);

        builder.Property(d => d.DocumentType)
            .HasMaxLength(100);

        builder.Property(d => d.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(d => d.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation configured in VehicleConfiguration
    }
}
