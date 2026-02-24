using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration
{
    public class ShoppingLocationConfiguration : IEntityTypeConfiguration<ShoppingLocation>
    {
        public void Configure(EntityTypeBuilder<ShoppingLocation> builder)
        {
            builder.ToTable("shopping_locations");

            builder.HasKey(sl => sl.Id);

            builder.Property(sl => sl.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(sl => sl.TenantId)
                .HasColumnName("tenant_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(sl => sl.Name)
                .HasColumnName("name")
                .HasColumnType("character varying(255)")
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(sl => sl.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            // Store Integration fields
            builder.Property(sl => sl.IntegrationType)
                .HasColumnName("integration_type")
                .HasColumnType("character varying(50)");

            builder.Property(sl => sl.ExternalLocationId)
                .HasColumnName("external_location_id")
                .HasColumnType("character varying(100)");

            builder.Property(sl => sl.ExternalChainId)
                .HasColumnName("external_chain_id")
                .HasColumnType("character varying(100)");

            builder.Property(sl => sl.OAuthAccessToken)
                .HasColumnName("oauth_access_token")
                .HasColumnType("text");

            builder.Property(sl => sl.OAuthRefreshToken)
                .HasColumnName("oauth_refresh_token")
                .HasColumnType("text");

            builder.Property(sl => sl.OAuthTokenExpiresAt)
                .HasColumnName("oauth_token_expires_at")
                .HasColumnType("timestamp with time zone");

            builder.Property(sl => sl.StoreAddress)
                .HasColumnName("store_address")
                .HasColumnType("character varying(500)");

            builder.Property(sl => sl.StorePhone)
                .HasColumnName("store_phone")
                .HasColumnType("character varying(50)");

            builder.Property(sl => sl.Latitude)
                .HasColumnName("latitude")
                .HasColumnType("double precision");

            builder.Property(sl => sl.Longitude)
                .HasColumnName("longitude")
                .HasColumnType("double precision");

            builder.Property(sl => sl.AisleOrder)
                .HasColumnName("aisle_order")
                .HasColumnType("jsonb");

            builder.Property(sl => sl.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(sl => sl.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(sl => sl.TenantId)
                .HasDatabaseName("ix_shopping_locations_tenant_id");

            // Unique constraint on (TenantId, Name) for multi-tenancy
            builder.HasIndex(sl => new { sl.TenantId, sl.Name })
                .IsUnique()
                .HasDatabaseName("ux_shopping_locations_tenant_name");

            // Index on integration type for filtering
            builder.HasIndex(sl => new { sl.TenantId, sl.IntegrationType })
                .HasDatabaseName("ix_shopping_locations_tenant_integration_type");

            // Navigation properties
            builder.HasMany(sl => sl.Products)
                .WithOne(p => p.ShoppingLocation)
                .HasForeignKey(p => p.ShoppingLocationId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(sl => sl.ProductStoreMetadata)
                .WithOne(psm => psm.ShoppingLocation)
                .HasForeignKey(psm => psm.ShoppingLocationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
