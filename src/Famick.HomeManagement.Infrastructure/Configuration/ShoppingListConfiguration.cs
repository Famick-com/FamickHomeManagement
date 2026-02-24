using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration
{
    public class ShoppingListConfiguration : IEntityTypeConfiguration<ShoppingList>
    {
        public void Configure(EntityTypeBuilder<ShoppingList> builder)
        {
            builder.ToTable("shopping_lists");

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

            builder.Property(sl => sl.ShoppingLocationId)
                .HasColumnName("shopping_location_id")
                .HasColumnType("uuid")
                .IsRequired();

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
                .HasDatabaseName("ix_shopping_lists_tenant_id");

            builder.HasIndex(sl => sl.ShoppingLocationId)
                .HasDatabaseName("ix_shopping_lists_shopping_location_id");

            // Unique constraint on (TenantId, ShoppingLocationId, Name) for multi-tenancy
            builder.HasIndex(sl => new { sl.TenantId, sl.ShoppingLocationId, sl.Name })
                .IsUnique()
                .HasDatabaseName("ux_shopping_lists_tenant_location_name");

            // Navigation properties
            builder.HasOne(sl => sl.ShoppingLocation)
                .WithMany()
                .HasForeignKey(sl => sl.ShoppingLocationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_shopping_lists_shopping_locations");

            builder.HasMany(sl => sl.Items)
                .WithOne(sli => sli.ShoppingList)
                .HasForeignKey(sli => sli.ShoppingListId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
