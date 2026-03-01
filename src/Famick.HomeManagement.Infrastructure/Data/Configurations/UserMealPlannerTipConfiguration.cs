using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class UserMealPlannerTipConfiguration : IEntityTypeConfiguration<UserMealPlannerTip>
{
    public void Configure(EntityTypeBuilder<UserMealPlannerTip> builder)
    {
        builder.ToTable("user_meal_planner_tips");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.TipKey)
            .HasColumnName("tip_key")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.DismissedAt)
            .HasColumnName("dismissed_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one tip per user per key
        builder.HasIndex(t => new { t.UserId, t.TipKey })
            .IsUnique()
            .HasDatabaseName("ux_user_meal_planner_tips_user_key");

        // Foreign keys
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_meal_planner_tips_user");
    }
}
