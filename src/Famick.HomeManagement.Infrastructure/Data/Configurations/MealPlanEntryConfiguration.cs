using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class MealPlanEntryConfiguration : IEntityTypeConfiguration<MealPlanEntry>
{
    public void Configure(EntityTypeBuilder<MealPlanEntry> builder)
    {
        builder.ToTable("meal_plan_entries", t =>
        {
            // Exactly one of meal_id or inline_note must be set
            t.HasCheckConstraint(
                "ck_meal_plan_entries_meal_or_note",
                "(meal_id IS NOT NULL AND inline_note IS NULL) OR (meal_id IS NULL AND inline_note IS NOT NULL)");

            // IsBatchSource and BatchSourceEntryId are mutually exclusive
            t.HasCheckConstraint(
                "ck_meal_plan_entries_batch_exclusive",
                "NOT (is_batch_source = true AND batch_source_entry_id IS NOT NULL)");

            // Batch fields require a meal reference
            t.HasCheckConstraint(
                "ck_meal_plan_entries_batch_requires_meal",
                "(is_batch_source = false AND batch_source_entry_id IS NULL) OR meal_id IS NOT NULL");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.MealPlanId)
            .HasColumnName("meal_plan_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.MealId)
            .HasColumnName("meal_id")
            .HasColumnType("uuid");

        builder.Property(e => e.InlineNote)
            .HasColumnName("inline_note")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200);

        builder.Property(e => e.MealTypeId)
            .HasColumnName("meal_type_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.DayOfWeek)
            .HasColumnName("day_of_week")
            .IsRequired();

        builder.Property(e => e.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.IsBatchSource)
            .HasColumnName("is_batch_source")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.BatchSourceEntryId)
            .HasColumnName("batch_source_entry_id")
            .HasColumnType("uuid");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(e => e.MealPlanId)
            .HasDatabaseName("ix_meal_plan_entries_plan_id");

        builder.HasIndex(e => new { e.MealPlanId, e.DayOfWeek, e.MealTypeId })
            .HasDatabaseName("ix_meal_plan_entries_plan_day_type");

        // Foreign keys
        builder.HasOne(e => e.MealPlan)
            .WithMany(mp => mp.Entries)
            .HasForeignKey(e => e.MealPlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_meal_plan_entries_plan");

        builder.HasOne(e => e.Meal)
            .WithMany(m => m.MealPlanEntries)
            .HasForeignKey(e => e.MealId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_meal_plan_entries_meal");

        builder.HasOne(e => e.MealType)
            .WithMany(mt => mt.MealPlanEntries)
            .HasForeignKey(e => e.MealTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_meal_plan_entries_meal_type");

        // Self-referencing FK for batch cooking
        builder.HasOne(e => e.BatchSourceEntry)
            .WithMany(e => e.BatchDependentEntries)
            .HasForeignKey(e => e.BatchSourceEntryId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_meal_plan_entries_batch_source");
    }
}
