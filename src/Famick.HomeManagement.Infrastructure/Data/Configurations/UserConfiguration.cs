using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // A sign-in email names one person across every household, stored in the form
        // EmailAddress.Normalize produces, so the unique index below cannot be dodged by case.
        builder.ToTable("users", t => t.HasCheckConstraint(
            "CK_users_Email_normalized", "\"Email\" = lower(btrim(\"Email\"))"));

        builder.HasKey(u => u.Id);

        builder.Property(u => u.TenantId)
            .IsRequired();

        builder.HasIndex(u => u.TenantId);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(u => new { u.TenantId, u.Username })
            .IsUnique();

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        // Terms acceptance (cloud legal consent)
        builder.Property(u => u.TermsVersion)
            .HasMaxLength(50);

        builder.Property(u => u.TermsAcceptedIpAddress)
            .HasMaxLength(45);

        // Phase 4 chunk 4.D — canonical scheme://host[:port] form; matches
        // the LocalServer string returned on LoginResponse.
        builder.Property(u => u.LastDeliveredLocalServer)
            .HasMaxLength(512);

        // Navigation
//         builder.HasOne(u => u.Tenant)
//             .WithMany(t => t.Users)
//             .HasForeignKey(u => u.TenantId)
//             .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserPermissions)
            .WithOne(up => up.User)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Contact FK (1:1 relationship)
        builder.HasOne(u => u.Contact)
            .WithOne(c => c.LinkedUser)
            .HasForeignKey<User>(u => u.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
