using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class CalendarEventMemberConfiguration : IEntityTypeConfiguration<CalendarEventMember>
{
    public void Configure(EntityTypeBuilder<CalendarEventMember> builder)
    {
        builder.ToTable("calendar_event_members");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(m => m.CalendarEventId)
            .HasColumnName("calendar_event_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(m => m.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(m => m.ParticipationType)
            .HasColumnName("participation_type")
            .IsRequired()
            .HasDefaultValue(ParticipationType.Involved)
            .HasSentinel((ParticipationType)0);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: a user can only be a member once per event
        builder.HasIndex(m => new { m.CalendarEventId, m.UserId })
            .IsUnique()
            .HasDatabaseName("ix_calendar_event_members_event_user");

        // Foreign keys
        builder.HasOne(m => m.CalendarEvent)
            .WithMany(e => e.Members)
            .HasForeignKey(m => m.CalendarEventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_calendar_event_members_event");

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_calendar_event_members_user");
    }
}
