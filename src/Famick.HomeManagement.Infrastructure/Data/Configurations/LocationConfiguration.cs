using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId)
            .IsRequired();

        builder.HasIndex(l => l.TenantId);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(l => new { l.TenantId, l.Name })
            .IsUnique();

        builder.Property(l => l.Description)
            .HasMaxLength(1000);

        builder.Property(l => l.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(l => l.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(l => l.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation
        builder.HasMany(l => l.Products)
            .WithOne(p => p.Location)
            .HasForeignKey(p => p.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
