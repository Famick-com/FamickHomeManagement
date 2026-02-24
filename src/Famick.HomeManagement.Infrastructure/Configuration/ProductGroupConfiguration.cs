using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration
{
    public class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
    {
        public void Configure(EntityTypeBuilder<ProductGroup> builder)
        {
            builder.ToTable("product_groups");

            builder.HasKey(pg => pg.Id);

            builder.Property(pg => pg.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(pg => pg.TenantId)
                .HasColumnName("tenant_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(pg => pg.Name)
                .HasColumnName("name")
                .HasColumnType("character varying(255)")
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(pg => pg.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            builder.Property(pg => pg.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(pg => pg.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(pg => pg.TenantId)
                .HasDatabaseName("ix_product_groups_tenant_id");

            // Unique constraint on (TenantId, Name) for multi-tenancy
            builder.HasIndex(pg => new { pg.TenantId, pg.Name })
                .IsUnique()
                .HasDatabaseName("ux_product_groups_tenant_name");

            // Navigation properties
            builder.HasMany(pg => pg.Products)
                .WithOne(p => p.ProductGroup)
                .HasForeignKey(p => p.ProductGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
