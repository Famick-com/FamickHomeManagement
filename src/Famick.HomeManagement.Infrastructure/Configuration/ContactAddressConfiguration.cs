using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ContactAddressConfiguration : IEntityTypeConfiguration<ContactAddress>
{
    public void Configure(EntityTypeBuilder<ContactAddress> builder)
    {
        builder.ToTable("contact_addresses");

        builder.HasKey(ca => ca.Id);

        builder.Property(ca => ca.TenantId)
            .IsRequired();

        builder.HasIndex(ca => ca.TenantId);

        builder.Property(ca => ca.Tag)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(AddressTag.Home);

        builder.Property(ca => ca.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(ca => ca.Label)
            .HasMaxLength(100);

        builder.Property(ca => ca.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Composite unique index - a contact can only have one address per AddressId
        builder.HasIndex(ca => new { ca.ContactId, ca.AddressId })
            .IsUnique();

        // Index for finding primary address
        builder.HasIndex(ca => new { ca.ContactId, ca.IsPrimary });

        // FK to Contact
        builder.HasOne(ca => ca.Contact)
            .WithMany(c => c.Addresses)
            .HasForeignKey(ca => ca.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Address (shared, not tenant-scoped)
        builder.HasOne(ca => ca.Address)
            .WithMany()
            .HasForeignKey(ca => ca.AddressId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
