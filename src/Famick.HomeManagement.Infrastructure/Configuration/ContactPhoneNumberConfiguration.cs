using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ContactPhoneNumberConfiguration : IEntityTypeConfiguration<ContactPhoneNumber>
{
    public void Configure(EntityTypeBuilder<ContactPhoneNumber> builder)
    {
        builder.ToTable("contact_phone_numbers");

        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.TenantId)
            .IsRequired();

        builder.HasIndex(cp => cp.TenantId);

        builder.Property(cp => cp.PhoneNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(cp => cp.NormalizedNumber)
            .HasMaxLength(30);

        builder.Property(cp => cp.Tag)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(PhoneTag.Mobile);

        builder.Property(cp => cp.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(cp => cp.Label)
            .HasMaxLength(100);

        builder.Property(cp => cp.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(cp => new { cp.ContactId, cp.IsPrimary });
        builder.HasIndex(cp => new { cp.TenantId, cp.NormalizedNumber });

        // FK to Contact
        builder.HasOne(cp => cp.Contact)
            .WithMany(c => c.PhoneNumbers)
            .HasForeignKey(cp => cp.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
