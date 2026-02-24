using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration
{
    public class ShoppingListItemConfiguration : IEntityTypeConfiguration<ShoppingListItem>
    {
        public void Configure(EntityTypeBuilder<ShoppingListItem> builder)
        {
            builder.ToTable("shopping_list");

            builder.HasKey(sli => sli.Id);

            builder.Property(sli => sli.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(sli => sli.TenantId)
                .HasColumnName("tenant_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(sli => sli.ShoppingListId)
                .HasColumnName("shopping_list_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(sli => sli.ProductId)
                .HasColumnName("product_id")
                .HasColumnType("uuid");

            builder.Property(sli => sli.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(sli => sli.PurchasedQuantity)
                .HasColumnName("purchased_quantity")
                .HasColumnType("numeric(18,4)")
                .IsRequired()
                .HasDefaultValue(0m);

            builder.Property(sli => sli.Note)
                .HasColumnName("note")
                .HasColumnType("text");

            builder.Property(sli => sli.IsPurchased)
                .HasColumnName("is_purchased")
                .HasColumnType("boolean")
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(sli => sli.PurchasedAt)
                .HasColumnName("purchased_at")
                .HasColumnType("timestamp with time zone");

            builder.Property(sli => sli.BestBeforeDate)
                .HasColumnName("best_before_date")
                .HasColumnType("timestamp with time zone");

            builder.Property(sli => sli.Aisle)
                .HasColumnName("aisle")
                .HasColumnType("character varying(50)")
                .HasMaxLength(50);

            builder.Property(sli => sli.Shelf)
                .HasColumnName("shelf")
                .HasColumnType("character varying(50)")
                .HasMaxLength(50);

            builder.Property(sli => sli.Department)
                .HasColumnName("department")
                .HasColumnType("character varying(100)")
                .HasMaxLength(100);

            builder.Property(sli => sli.ExternalProductId)
                .HasColumnName("external_product_id")
                .HasColumnType("character varying(100)")
                .HasMaxLength(100);

            builder.Property(sli => sli.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(sli => sli.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(sli => sli.TenantId)
                .HasDatabaseName("ix_shopping_list_tenant_id");

            builder.HasIndex(sli => sli.ShoppingListId)
                .HasDatabaseName("ix_shopping_list_shopping_list_id");

            builder.HasIndex(sli => sli.ProductId)
                .HasDatabaseName("ix_shopping_list_product_id");

            // Index for purchased filter
            builder.HasIndex(sli => new { sli.ShoppingListId, sli.IsPurchased })
                .HasDatabaseName("ix_shopping_list_purchased");

            // Foreign keys
            builder.HasOne(sli => sli.ShoppingList)
                .WithMany(sl => sl.Items)
                .HasForeignKey(sli => sli.ShoppingListId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_shopping_list_shopping_lists");

            builder.HasOne(sli => sli.Product)
                .WithMany()
                .HasForeignKey(sli => sli.ProductId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_shopping_list_products");
        }
    }
}
