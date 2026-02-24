using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for UserPasskeyCredential entity
/// </summary>
public class UserPasskeyCredentialConfiguration : IEntityTypeConfiguration<UserPasskeyCredential>
{
    public void Configure(EntityTypeBuilder<UserPasskeyCredential> builder)
    {
        builder.ToTable("user_passkey_credentials");

        builder.HasKey(upc => upc.Id);

        builder.Property(upc => upc.TenantId)
            .IsRequired();

        builder.Property(upc => upc.UserId)
            .IsRequired();

        builder.Property(upc => upc.CredentialId)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(upc => upc.PublicKey)
            .IsRequired()
            .HasMaxLength(4096);

        builder.Property(upc => upc.SignatureCounter)
            .IsRequired();

        builder.Property(upc => upc.DeviceName)
            .HasMaxLength(255);

        builder.Property(upc => upc.AaGuid)
            .HasMaxLength(100);

        builder.Property(upc => upc.CredentialType)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("public-key");

        builder.Property(upc => upc.UserVerification)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(upc => upc.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(upc => upc.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(upc => upc.TenantId);
        builder.HasIndex(upc => upc.UserId);

        // Credential ID must be unique within a tenant
        builder.HasIndex(upc => new { upc.TenantId, upc.CredentialId })
            .IsUnique();

        // Relationships
        builder.HasOne(upc => upc.User)
            .WithMany(u => u.PasskeyCredentials)
            .HasForeignKey(upc => upc.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
