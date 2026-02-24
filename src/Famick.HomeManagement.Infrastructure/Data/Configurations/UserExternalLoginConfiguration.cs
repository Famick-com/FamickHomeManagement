using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for UserExternalLogin entity
/// </summary>
public class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> builder)
    {
        builder.ToTable("user_external_logins");

        builder.HasKey(uel => uel.Id);

        builder.Property(uel => uel.TenantId)
            .IsRequired();

        builder.Property(uel => uel.UserId)
            .IsRequired();

        builder.Property(uel => uel.Provider)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(uel => uel.ProviderUserId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(uel => uel.ProviderDisplayName)
            .HasMaxLength(500);

        builder.Property(uel => uel.ProviderEmail)
            .HasMaxLength(255);

        builder.Property(uel => uel.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(uel => uel.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(uel => uel.TenantId);
        builder.HasIndex(uel => uel.UserId);

        // One link per provider per user within a tenant
        builder.HasIndex(uel => new { uel.TenantId, uel.UserId, uel.Provider })
            .IsUnique();

        // Prevent duplicate provider accounts within a tenant
        builder.HasIndex(uel => new { uel.TenantId, uel.Provider, uel.ProviderUserId })
            .IsUnique();

        // Relationships
        builder.HasOne(uel => uel.User)
            .WithMany(u => u.ExternalLogins)
            .HasForeignKey(uel => uel.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
