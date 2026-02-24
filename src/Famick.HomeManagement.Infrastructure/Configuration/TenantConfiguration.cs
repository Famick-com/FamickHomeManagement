using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(255)")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.KmsKeyId)
            .HasColumnName("kms_key_id")
            .HasColumnType("character varying(256)")
            .HasMaxLength(256);

        builder.Property(t => t.AddressId)
            .HasColumnName("address_id")
            .HasColumnType("uuid");

        // Audit timestamps
        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // FK to Address
        builder.HasOne(t => t.Address)
            .WithMany()
            .HasForeignKey(t => t.AddressId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(t => t.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("America/New_York");

        builder.Property(t => t.DefaultEventColor)
            .HasColumnName("default_event_color")
            .HasColumnType("character varying(20)")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("#4CAF50");

        // --- Cloud billing columns (unused in self-hosted mode) ---

        // Subscription
        builder.Property(t => t.SubscriptionTier)
            .HasColumnName("subscription_tier")
            .HasDefaultValue(SubscriptionTier.Free);

        builder.Property(t => t.MaxUsers)
            .HasColumnName("max_users")
            .HasDefaultValue(5);

        builder.Property(t => t.StorageQuotaMb)
            .HasColumnName("storage_quota_mb")
            .HasDefaultValue(1000);

        builder.Property(t => t.SubscriptionExpiresAt)
            .HasColumnName("subscription_expires_at")
            .HasColumnType("timestamp with time zone");

        // Trial
        builder.Property(t => t.TrialStartedAt)
            .HasColumnName("trial_started_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.TrialEndsAt)
            .HasColumnName("trial_ends_at")
            .HasColumnType("timestamp with time zone");

        builder.Ignore(t => t.IsTrialActive);

        // Storage
        builder.Property(t => t.StorageBlocksPurchased)
            .HasColumnName("storage_blocks_purchased")
            .HasDefaultValue(0);

        builder.Property(t => t.StorageUsedBytes)
            .HasColumnName("storage_used_bytes")
            .HasDefaultValue(0L);

        // Stripe
        builder.Property(t => t.StripeCustomerId)
            .HasColumnName("stripe_customer_id")
            .HasMaxLength(255);

        builder.Property(t => t.StripeSubscriptionId)
            .HasColumnName("stripe_subscription_id")
            .HasMaxLength(255);

        // RevenueCat
        builder.Property(t => t.RevenueCatUserId)
            .HasColumnName("revenuecat_user_id")
            .HasMaxLength(255);
    }
}
