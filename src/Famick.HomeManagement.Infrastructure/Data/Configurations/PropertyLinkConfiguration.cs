using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class PropertyLinkConfiguration : IEntityTypeConfiguration<PropertyLink>
{
    public void Configure(EntityTypeBuilder<PropertyLink> builder)
    {
        builder.ToTable("property_links");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.HasIndex(p => p.TenantId);

        builder.Property(p => p.HomeId)
            .IsRequired();

        builder.HasIndex(p => p.HomeId);

        builder.Property(p => p.Url)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.Label)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation to Home
        builder.HasOne(p => p.Home)
            .WithMany(h => h.PropertyLinks)
            .HasForeignKey(p => p.HomeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
