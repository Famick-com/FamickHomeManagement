using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for TenantIntegrationToken entity
/// </summary>
public class TenantIntegrationTokenConfiguration : IEntityTypeConfiguration<TenantIntegrationToken>
{
    public void Configure(EntityTypeBuilder<TenantIntegrationToken> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.PluginId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.AccessToken)
            .HasMaxLength(4000);

        builder.Property(t => t.RefreshToken)
            .HasMaxLength(4000);

        builder.Property(t => t.RequiresReauth)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one token per tenant per plugin
        builder.HasIndex(t => new { t.TenantId, t.PluginId })
            .IsUnique();

        // Index for efficient lookups
        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.PluginId);
    }
}
