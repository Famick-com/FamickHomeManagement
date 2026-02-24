using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class ExternalCalendarEventConfiguration : IEntityTypeConfiguration<ExternalCalendarEvent>
{
    public void Configure(EntityTypeBuilder<ExternalCalendarEvent> builder)
    {
        builder.ToTable("external_calendar_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.SubscriptionId)
            .HasColumnName("subscription_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.ExternalUid)
            .HasColumnName("external_uid")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500)
            .IsRequired();

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

        // Unique constraint: one event per external UID per subscription
        builder.HasIndex(e => new { e.SubscriptionId, e.ExternalUid })
            .IsUnique()
            .HasDatabaseName("ix_external_calendar_events_subscription_uid");

        // Index for date range queries
        builder.HasIndex(e => new { e.SubscriptionId, e.StartTimeUtc })
            .HasDatabaseName("ix_external_calendar_events_subscription_start");

        // Foreign key
        builder.HasOne(e => e.Subscription)
            .WithMany(s => s.Events)
            .HasForeignKey(e => e.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_external_calendar_events_subscription");
    }
}
