namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Subscription tiers for the cloud SaaS offering.
/// In self-hosted mode, this enum exists but is unused (tenants have no subscription tier).
/// </summary>
public enum SubscriptionTier
{
    /// <summary>
    /// Free trial tier - 30 days of Home-level features, then read-only
    /// </summary>
    Free = 0,

    /// <summary>
    /// Organize tier: $3.99/mo - contacts, chores, equipment, todos
    /// </summary>
    Organize = 1,

    /// <summary>
    /// Home tier: $8.99/mo - adds shopping, inventory, recipes, vehicles
    /// </summary>
    Home = 2,

    /// <summary>
    /// Pro tier: $16.99/mo - full suite with analytics, data export, API access
    /// </summary>
    Pro = 3
}
