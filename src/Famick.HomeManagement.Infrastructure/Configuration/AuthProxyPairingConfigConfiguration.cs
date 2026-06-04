using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class AuthProxyPairingConfigConfiguration : IEntityTypeConfiguration<AuthProxyPairingConfig>
{
    public void Configure(EntityTypeBuilder<AuthProxyPairingConfig> builder)
    {
        builder.ToTable("auth_proxy_pairing_configs");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.AuthProxyHomeServerId)
            .HasColumnName("auth_proxy_home_server_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.AuthProxyBaseUrl)
            .HasColumnName("auth_proxy_base_url")
            .HasColumnType("character varying(2048)")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(p => p.PairedAdminEmail)
            .HasColumnName("paired_admin_email")
            .HasColumnType("character varying(320)")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(p => p.DisplayName)
            .HasColumnName("display_name")
            .HasColumnType("character varying(256)")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.PairedAt)
            .HasColumnName("paired_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // One pairing config per tenant. Self-hosted single-tenant has
        // at most one row total; multi-tenant cloud has one per tenant.
        builder.HasIndex(p => p.TenantId).IsUnique();
    }
}
