using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.ToTable("recipe_images");

        builder.HasKey(ri => ri.Id);

        builder.Property(ri => ri.TenantId)
            .IsRequired();

        builder.HasIndex(ri => ri.TenantId);

        builder.HasIndex(ri => new { ri.TenantId, ri.RecipeId });

        builder.Property(ri => ri.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(ri => ri.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(ri => ri.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ri => ri.FileSize)
            .IsRequired();

        builder.Property(ri => ri.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(ri => ri.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(ri => ri.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
