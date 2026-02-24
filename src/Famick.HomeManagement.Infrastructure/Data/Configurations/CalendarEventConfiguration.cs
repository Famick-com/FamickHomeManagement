using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(e => e.Location)
            .HasColumnName("location")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(e => e.StartTimeUtc)
            .HasColumnName("start_time_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.EndTimeUtc)
            .HasColumnName("end_time_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.IsAllDay)
            .HasColumnName("is_all_day")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.RecurrenceRule)
            .HasColumnName("recurrence_rule")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(e => e.RecurrenceEndDate)
            .HasColumnName("recurrence_end_date")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.ReminderMinutesBefore)
            .HasColumnName("reminder_minutes_before");

        builder.Property(e => e.Color)
            .HasColumnName("color")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(e => e.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_calendar_events_tenant_id");

        builder.HasIndex(e => new { e.TenantId, e.StartTimeUtc })
            .HasDatabaseName("ix_calendar_events_tenant_start");

        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("ix_calendar_events_created_by");

        // Foreign keys
        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_calendar_events_created_by_user");

        // Collections configured from child side
    }
}
