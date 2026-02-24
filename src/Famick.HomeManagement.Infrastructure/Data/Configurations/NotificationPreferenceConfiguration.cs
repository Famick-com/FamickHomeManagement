using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.HasIndex(p => p.TenantId);

        builder.Property(p => p.UserId)
            .IsRequired();

        builder.Property(p => p.NotificationType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.EmailEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.PushEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.InAppEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(p => new { p.TenantId, p.UserId, p.NotificationType })
            .IsUnique()
            .HasDatabaseName("ix_notification_preferences_tenant_user_type");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
