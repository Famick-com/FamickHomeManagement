using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ContactSocialMediaConfiguration : IEntityTypeConfiguration<ContactSocialMedia>
{
    public void Configure(EntityTypeBuilder<ContactSocialMedia> builder)
    {
        builder.ToTable("contact_social_media");

        builder.HasKey(cs => cs.Id);

        builder.Property(cs => cs.TenantId)
            .IsRequired();

        builder.HasIndex(cs => cs.TenantId);

        builder.Property(cs => cs.Service)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(cs => cs.Username)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(cs => cs.ProfileUrl)
            .HasMaxLength(500);

        builder.Property(cs => cs.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint - one username per service per contact
        builder.HasIndex(cs => new { cs.ContactId, cs.Service })
            .IsUnique();

        // FK to Contact
        builder.HasOne(cs => cs.Contact)
            .WithMany(c => c.SocialMedia)
            .HasForeignKey(cs => cs.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
