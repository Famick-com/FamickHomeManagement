using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration
{
    public class StockLogConfiguration : IEntityTypeConfiguration<StockLog>
    {
        public void Configure(EntityTypeBuilder<StockLog> builder)
        {
            builder.ToTable("stock_log");

            builder.HasKey(sl => sl.Id);

            builder.Property(sl => sl.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(sl => sl.TenantId)
                .HasColumnName("tenant_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(sl => sl.ProductId)
                .HasColumnName("product_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(sl => sl.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            builder.Property(sl => sl.BestBeforeDate)
                .HasColumnName("best_before_date")
                .HasColumnType("timestamp with time zone");

            builder.Property(sl => sl.PurchasedDate)
                .HasColumnName("purchased_date")
                .HasColumnType("timestamp with time zone");

            builder.Property(sl => sl.UsedDate)
                .HasColumnName("used_date")
                .HasColumnType("timestamp with time zone");

            builder.Property(sl => sl.Spoiled)
                .HasColumnName("spoiled")
                .HasColumnType("integer")
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(sl => sl.StockId)
                .HasColumnName("stock_id")
                .HasColumnType("character varying(100)")
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(sl => sl.TransactionType)
                .HasColumnName("transaction_type")
                .HasColumnType("character varying(50)")
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(sl => sl.Price)
                .HasColumnName("price")
                .HasColumnType("numeric(18,4)");

            builder.Property(sl => sl.Undone)
                .HasColumnName("undone")
                .HasColumnType("boolean")
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(sl => sl.UndoneTimestamp)
                .HasColumnName("undone_timestamp")
                .HasColumnType("timestamp with time zone");

            builder.Property(sl => sl.OpenedDate)
                .HasColumnName("opened_date")
                .HasColumnType("timestamp with time zone");

            builder.Property(sl => sl.LocationId)
                .HasColumnName("location_id")
                .HasColumnType("uuid");

            builder.Property(sl => sl.RecipeId)
                .HasColumnName("recipe_id")
                .HasColumnType("uuid");

            builder.Property(sl => sl.CorrelationId)
                .HasColumnName("correlation_id")
                .HasColumnType("character varying(100)")
                .HasMaxLength(100);

            builder.Property(sl => sl.TransactionId)
                .HasColumnName("transaction_id")
                .HasColumnType("character varying(100)")
                .HasMaxLength(100);

            builder.Property(sl => sl.StockRowId)
                .HasColumnName("stock_row_id")
                .HasColumnType("uuid");

            builder.Property(sl => sl.ShoppingLocationId)
                .HasColumnName("shopping_location_id")
                .HasColumnType("uuid");

            builder.Property(sl => sl.UserId)
                .HasColumnName("user_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(sl => sl.Note)
                .HasColumnName("note")
                .HasColumnType("text");

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
                .HasDatabaseName("ix_stock_log_tenant_id");

            builder.HasIndex(sl => new { sl.StockId, sl.TransactionType, sl.Amount })
                .HasDatabaseName("ix_stock_log_performance1");

            builder.HasIndex(sl => new { sl.ProductId, sl.BestBeforeDate, sl.PurchasedDate, sl.TransactionType, sl.StockId, sl.Undone })
                .HasDatabaseName("ix_stock_log_performance2");

            builder.HasIndex(sl => sl.CorrelationId)
                .HasDatabaseName("ix_stock_log_correlation_id");

            builder.HasIndex(sl => sl.TransactionId)
                .HasDatabaseName("ix_stock_log_transaction_id");

            builder.HasIndex(sl => sl.UserId)
                .HasDatabaseName("ix_stock_log_user_id");

            // Foreign keys
            builder.HasOne(sl => sl.Product)
                .WithMany()
                .HasForeignKey(sl => sl.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_stock_log_products");

            builder.HasOne(sl => sl.Location)
                .WithMany()
                .HasForeignKey(sl => sl.LocationId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_stock_log_locations");

            builder.HasOne(sl => sl.User)
                .WithMany()
                .HasForeignKey(sl => sl.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_stock_log_users");

            builder.HasOne(sl => sl.StockRow)
                .WithMany()
                .HasForeignKey(sl => sl.StockRowId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_stock_log_stock");
        }
    }
}
