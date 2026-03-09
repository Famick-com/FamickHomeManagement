using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class MasterProductImageConfiguration : IEntityTypeConfiguration<MasterProductImage>
{
    public void Configure(EntityTypeBuilder<MasterProductImage> builder)
    {
        builder.ToTable("master_product_images");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.MasterProductId).HasColumnName("master_product_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.FileName).HasColumnName("file_name").HasColumnType("character varying(500)").IsRequired().HasMaxLength(500);
        builder.Property(i => i.OriginalFileName).HasColumnName("original_file_name").HasColumnType("character varying(500)").IsRequired().HasMaxLength(500);
        builder.Property(i => i.ContentType).HasColumnName("content_type").HasColumnType("character varying(100)").IsRequired().HasMaxLength(100);
        builder.Property(i => i.FileSize).HasColumnName("file_size").HasColumnType("bigint").IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").HasColumnType("integer").IsRequired().HasDefaultValue(0);
        builder.Property(i => i.IsPrimary).HasColumnName("is_primary").HasColumnType("boolean").IsRequired().HasDefaultValue(false);
        builder.Property(i => i.ExternalUrl).HasColumnName("external_url").HasColumnType("text");
        builder.Property(i => i.ExternalThumbnailUrl).HasColumnName("external_thumbnail_url").HasColumnType("text");
        builder.Property(i => i.ExternalSource).HasColumnName("external_source").HasColumnType("character varying(100)").HasMaxLength(100);

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

        builder.HasOne(i => i.MasterProduct)
            .WithMany(mp => mp.Images)
            .HasForeignKey(i => i.MasterProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
