using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration
{
    public class ChoreConfiguration : IEntityTypeConfiguration<Chore>
    {
        public void Configure(EntityTypeBuilder<Chore> builder)
        {
            builder.ToTable("chores");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(c => c.TenantId)
                .HasColumnName("tenant_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(c => c.Name)
                .HasColumnName("name")
                .HasColumnType("character varying(255)")
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(c => c.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            builder.Property(c => c.PeriodType)
                .HasColumnName("period_type")
                .HasColumnType("character varying(50)")
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(c => c.PeriodDays)
                .HasColumnName("period_days")
                .HasColumnType("integer");

            builder.Property(c => c.TrackDateOnly)
                .HasColumnName("track_date_only")
                .HasColumnType("smallint")
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(c => c.Rollover)
                .HasColumnName("rollover")
                .HasColumnType("smallint")
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(c => c.AssignmentType)
                .HasColumnName("assignment_type")
                .HasColumnType("character varying(50)")
                .HasMaxLength(50);

            builder.Property(c => c.AssignmentConfig)
                .HasColumnName("assignment_config")
                .HasColumnType("text");

            builder.Property(c => c.NextExecutionAssignedToUserId)
                .HasColumnName("next_execution_assigned_to_user_id")
                .HasColumnType("uuid");

            builder.Property(c => c.ConsumeProductOnExecution)
                .HasColumnName("consume_product_on_execution")
                .HasColumnType("smallint")
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(c => c.ProductId)
                .HasColumnName("product_id")
                .HasColumnType("uuid");

            builder.Property(c => c.ProductAmount)
                .HasColumnName("product_amount")
                .HasColumnType("numeric(18,4)");

            builder.Property(c => c.EquipmentId)
                .HasColumnName("equipment_id")
                .HasColumnType("uuid");

            builder.Property(c => c.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(c => c.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(c => c.TenantId)
                .HasDatabaseName("ix_chores_tenant_id");

            // Unique constraint on (TenantId, Name) for multi-tenancy
            builder.HasIndex(c => new { c.TenantId, c.Name })
                .IsUnique()
                .HasDatabaseName("ux_chores_tenant_name");

            // Foreign keys
            builder.HasOne(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_chores_products");

            builder.HasOne(c => c.NextExecutionAssignedToUser)
                .WithMany()
                .HasForeignKey(c => c.NextExecutionAssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_chores_users");

            builder.HasOne(c => c.Equipment)
                .WithMany(e => e.Chores)
                .HasForeignKey(c => c.EquipmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_chores_equipment");

            // Navigation properties
            builder.HasMany(c => c.LogEntries)
                .WithOne(cl => cl.Chore)
                .HasForeignKey(cl => cl.ChoreId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
