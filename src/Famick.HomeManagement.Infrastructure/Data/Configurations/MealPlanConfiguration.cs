using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class MealPlanConfiguration : IEntityTypeConfiguration<MealPlan>
{
    public void Configure(EntityTypeBuilder<MealPlan> builder)
    {
        builder.ToTable("meal_plans");

        builder.HasKey(mp => mp.Id);

        builder.Property(mp => mp.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mp => mp.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mp => mp.WeekStartDate)
            .HasColumnName("week_start_date")
            .IsRequired();

        builder.Property(mp => mp.UpdatedByUserId)
            .HasColumnName("updated_by_user_id")
            .HasColumnType("uuid");

        // Optimistic concurrency via PostgreSQL xmin system column
        builder.Property(mp => mp.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(mp => mp.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(mp => mp.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(mp => mp.TenantId)
            .HasDatabaseName("ix_meal_plans_tenant_id");

        // One plan per tenant per week
        builder.HasIndex(mp => new { mp.TenantId, mp.WeekStartDate })
            .IsUnique()
            .HasDatabaseName("ux_meal_plans_tenant_week");

        // Foreign keys
        builder.HasOne(mp => mp.UpdatedByUser)
            .WithMany()
            .HasForeignKey(mp => mp.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_meal_plans_updated_by_user");
    }
}
