using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class UserMealPlannerPreferenceConfiguration : IEntityTypeConfiguration<UserMealPlannerPreference>
{
    public void Configure(EntityTypeBuilder<UserMealPlannerPreference> builder)
    {
        builder.ToTable("user_meal_planner_preferences");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.HasCompletedOnboarding)
            .HasColumnName("has_completed_onboarding")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.PlanningStyle)
            .HasColumnName("planning_style");

        builder.Property(p => p.CollapsedMealTypeIds)
            .HasColumnName("collapsed_meal_type_ids")
            .HasColumnType("jsonb");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one preference record per user
        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasDatabaseName("ux_user_meal_planner_prefs_user");

        // Foreign keys
        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_meal_planner_prefs_user");
    }
}
