using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class AppMetadataConfiguration : IEntityTypeConfiguration<AppMetadata>
{
    public void Configure(EntityTypeBuilder<AppMetadata> builder)
    {
        builder.ToTable("app_metadata");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(m => m.Key)
            .HasColumnName("key")
            .HasColumnType("character varying(200)")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Value)
            .HasColumnName("value")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(m => m.Key)
            .IsUnique()
            .HasDatabaseName("ux_app_metadata_key");
    }
}
