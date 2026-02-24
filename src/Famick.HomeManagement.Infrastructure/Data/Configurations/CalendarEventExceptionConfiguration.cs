using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class CalendarEventExceptionConfiguration : IEntityTypeConfiguration<CalendarEventException>
{
    public void Configure(EntityTypeBuilder<CalendarEventException> builder)
    {
        builder.ToTable("calendar_event_exceptions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.CalendarEventId)
            .HasColumnName("calendar_event_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.OriginalStartTimeUtc)
            .HasColumnName("original_start_time_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.OverrideTitle)
            .HasColumnName("override_title")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(e => e.OverrideDescription)
            .HasColumnName("override_description")
            .HasColumnType("text");

        builder.Property(e => e.OverrideLocation)
            .HasColumnName("override_location")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(e => e.OverrideStartTimeUtc)
            .HasColumnName("override_start_time_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.OverrideEndTimeUtc)
            .HasColumnName("override_end_time_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.OverrideIsAllDay)
            .HasColumnName("override_is_all_day");

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

        // Index: find exceptions for a specific event
        builder.HasIndex(e => e.CalendarEventId)
            .HasDatabaseName("ix_calendar_event_exceptions_event_id");

        // Unique constraint: one exception per occurrence per event
        builder.HasIndex(e => new { e.CalendarEventId, e.OriginalStartTimeUtc })
            .IsUnique()
            .HasDatabaseName("ix_calendar_event_exceptions_event_occurrence");

        // Foreign key
        builder.HasOne(e => e.CalendarEvent)
            .WithMany(ce => ce.Exceptions)
            .HasForeignKey(e => e.CalendarEventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_calendar_event_exceptions_event");
    }
}
