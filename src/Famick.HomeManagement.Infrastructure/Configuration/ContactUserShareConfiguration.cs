using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ContactUserShareConfiguration : IEntityTypeConfiguration<ContactUserShare>
{
    public void Configure(EntityTypeBuilder<ContactUserShare> builder)
    {
        builder.ToTable("contact_user_shares");

        builder.HasKey(cus => cus.Id);

        builder.Property(cus => cus.TenantId)
            .IsRequired();

        builder.HasIndex(cus => cus.TenantId);

        builder.Property(cus => cus.CanEdit)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(cus => cus.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint - a contact can only be shared with a user once
        builder.HasIndex(cus => new { cus.ContactId, cus.SharedWithUserId })
            .IsUnique();

        // FK to Contact
        builder.HasOne(cus => cus.Contact)
            .WithMany(c => c.SharedWithUsers)
            .HasForeignKey(cus => cus.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to User
        builder.HasOne(cus => cus.SharedWithUser)
            .WithMany()
            .HasForeignKey(cus => cus.SharedWithUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
