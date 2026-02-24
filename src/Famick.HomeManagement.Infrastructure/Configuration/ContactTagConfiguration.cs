using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ContactTagConfiguration : IEntityTypeConfiguration<ContactTag>
{
    public void Configure(EntityTypeBuilder<ContactTag> builder)
    {
        builder.ToTable("contact_tags");

        builder.HasKey(ct => ct.Id);

        builder.Property(ct => ct.TenantId)
            .IsRequired();

        builder.HasIndex(ct => ct.TenantId);

        builder.Property(ct => ct.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ct => ct.Description)
            .HasMaxLength(500);

        builder.Property(ct => ct.Color)
            .HasMaxLength(20);

        builder.Property(ct => ct.Icon)
            .HasMaxLength(50);

        builder.Property(ct => ct.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique tag name per tenant
        builder.HasIndex(ct => new { ct.TenantId, ct.Name })
            .IsUnique();
    }
}
