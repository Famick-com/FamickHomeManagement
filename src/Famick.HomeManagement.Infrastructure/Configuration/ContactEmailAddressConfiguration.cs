using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ContactEmailAddressConfiguration : IEntityTypeConfiguration<ContactEmailAddress>
{
    public void Configure(EntityTypeBuilder<ContactEmailAddress> builder)
    {
        builder.ToTable("contact_email_addresses");

        builder.HasKey(ce => ce.Id);

        builder.Property(ce => ce.TenantId)
            .IsRequired();

        builder.HasIndex(ce => ce.TenantId);

        builder.Property(ce => ce.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(ce => ce.NormalizedEmail)
            .HasMaxLength(255);

        builder.Property(ce => ce.Tag)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(EmailTag.Personal);

        builder.Property(ce => ce.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(ce => ce.Label)
            .HasMaxLength(100);

        builder.Property(ce => ce.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(ce => new { ce.ContactId, ce.IsPrimary });
        builder.HasIndex(ce => new { ce.TenantId, ce.NormalizedEmail });

        // FK to Contact
        builder.HasOne(ce => ce.Contact)
            .WithMany(c => c.EmailAddresses)
            .HasForeignKey(ce => ce.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
