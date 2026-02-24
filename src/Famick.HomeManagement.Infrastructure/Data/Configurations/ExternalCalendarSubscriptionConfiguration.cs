using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class ExternalCalendarSubscriptionConfiguration : IEntityTypeConfiguration<ExternalCalendarSubscription>
{
    public void Configure(EntityTypeBuilder<ExternalCalendarSubscription> builder)
    {
        builder.ToTable("external_calendar_subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.IcsUrl)
            .HasColumnName("ics_url")
            .HasColumnType("character varying(2048)")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(s => s.Color)
            .HasColumnName("color")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(s => s.SyncIntervalMinutes)
            .HasColumnName("sync_interval_minutes")
            .IsRequired()
            .HasDefaultValue(60);

        builder.Property(s => s.LastSyncedAt)
            .HasColumnName("last_synced_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.LastSyncStatus)
            .HasColumnName("last_sync_status")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_external_calendar_subscriptions_tenant_id");

        builder.HasIndex(s => new { s.TenantId, s.UserId })
            .HasDatabaseName("ix_external_calendar_subscriptions_tenant_user");

        // Foreign keys
        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_external_calendar_subscriptions_user");
    }
}
