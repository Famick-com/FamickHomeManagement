using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ContactTagLinkConfiguration : IEntityTypeConfiguration<ContactTagLink>
{
    public void Configure(EntityTypeBuilder<ContactTagLink> builder)
    {
        builder.ToTable("contact_tag_links");

        builder.HasKey(ctl => ctl.Id);

        builder.Property(ctl => ctl.TenantId)
            .IsRequired();

        builder.HasIndex(ctl => ctl.TenantId);

        builder.Property(ctl => ctl.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint - a contact can only have a tag once
        builder.HasIndex(ctl => new { ctl.ContactId, ctl.TagId })
            .IsUnique();

        // FK to Contact
        builder.HasOne(ctl => ctl.Contact)
            .WithMany(c => c.Tags)
            .HasForeignKey(ctl => ctl.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Tag
        builder.HasOne(ctl => ctl.Tag)
            .WithMany(t => t.Contacts)
            .HasForeignKey(ctl => ctl.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
