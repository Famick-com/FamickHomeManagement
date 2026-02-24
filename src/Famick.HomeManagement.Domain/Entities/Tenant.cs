using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a tenant (household) in the system.
/// In self-hosted mode, there is exactly one tenant with the fixed ID.
/// In cloud SaaS mode, multiple tenants can exist with billing properties.
/// The Tenant's Id IS the TenantId used throughout BaseTenantEntity.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>
    /// Display name for the household (e.g., "The Smiths", "Beach House")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Subdomain for cloud multi-tenant routing (e.g., "acme" for acme.famick.com).
    /// Not used in self-hosted mode.
    /// </summary>
    public string? Subdomain { get; set; }

    /// <summary>
    /// Whether the tenant account is active.
    /// Inactive tenants cannot access the system. Defaults to true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// AWS KMS key ID used for per-tenant encryption of stored files.
    /// Only used in cloud deployments; null in self-hosted mode.
    /// </summary>
    public string? KmsKeyId { get; set; }

    /// <summary>
    /// Optional FK to the tenant's primary address
    /// </summary>
    public Guid? AddressId { get; set; }

    /// <summary>
    /// Navigation property to the tenant's address
    /// </summary>
    public virtual Address? Address { get; set; }

    /// <summary>
    /// IANA time zone identifier for the household (e.g., "America/New_York").
    /// Used for calendar display and reminder scheduling.
    /// </summary>
    public string TimeZoneId { get; set; } = "America/New_York";

    /// <summary>
    /// Default display color for household calendar events (hex, e.g. "#4CAF50").
    /// </summary>
    public string DefaultEventColor { get; set; } = "#4CAF50";

    /// <summary>
    /// JSON array of plugin IDs that an admin has disabled for this tenant.
    /// e.g., ["usda", "openfoodfacts"]. Null or empty means all plugins are enabled.
    /// </summary>
    public string? DisabledPluginIds { get; set; }

    // --- Cloud billing properties (unused in self-hosted mode) ---

    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;
    public int MaxUsers { get; set; } = 5;
    public int StorageQuotaMb { get; set; } = 1000;
    public DateTime? SubscriptionExpiresAt { get; set; }

    // Trial
    public DateTime? TrialStartedAt { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public bool IsTrialActive => TrialEndsAt.HasValue && TrialEndsAt.Value > DateTime.UtcNow;

    // Storage
    public int StorageBlocksPurchased { get; set; }
    public long StorageUsedBytes { get; set; }

    // Billing - Stripe
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }

    // Billing - RevenueCat (cross-platform mobile subscriptions)
    public string? RevenueCatUserId { get; set; }
}
