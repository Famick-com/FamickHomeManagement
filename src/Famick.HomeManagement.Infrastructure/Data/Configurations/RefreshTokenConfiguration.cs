using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for RefreshToken entity
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // Every family must be real. When the non-password sign-in paths left it at
        // Guid.Empty, one replay revoked every one of those sessions on the platform
        // (issue #106).
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_refresh_tokens_family_id_not_empty",
            "\"FamilyId\" <> '00000000-0000-0000-0000-000000000000'"));

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        builder.Property(rt => rt.DeviceInfo)
            .HasMaxLength(500);

        builder.Property(rt => rt.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(rt => rt.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(rt => rt.FamilyId)
            .IsRequired();

        builder.Property(rt => rt.AuthTime)
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(rt => rt.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(rt => rt.UserId);
        builder.HasIndex(rt => rt.TenantId);
        builder.HasIndex(rt => rt.TokenHash);
        builder.HasIndex(rt => rt.ExpiresAt);
        // FamilyId is the lookup key for reuse-detection bulk-revoke;
        // every refresh hits "find all active tokens in this family".
        builder.HasIndex(rt => rt.FamilyId);

        // Relationships
        builder.HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

//         builder.HasOne(rt => rt.Tenant)
//             .WithMany()
//             .HasForeignKey(rt => rt.TenantId)
//             .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rt => rt.ReplacedByToken)
            .WithMany()
            .HasForeignKey(rt => rt.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
