using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

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

        builder.HasIndex(u => new { u.TenantId, u.Email })
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

        // Terms acceptance (cloud legal consent)
        builder.Property(u => u.TermsVersion)
            .HasMaxLength(50);

        builder.Property(u => u.TermsAcceptedIpAddress)
            .HasMaxLength(45);

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
