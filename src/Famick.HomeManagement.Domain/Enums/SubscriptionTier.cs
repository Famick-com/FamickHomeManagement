namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Subscription tiers for the cloud SaaS offering.
/// In self-hosted mode, this enum exists but is unused (tenants have no subscription tier).
/// </summary>
/// <remarks>
/// Stored as an integer, so a value is never reused. 3 belonged to a retired tier; rows that
/// held it were moved to <see cref="Home"/>.
/// </remarks>
public enum SubscriptionTier
{
    /// <summary>
    /// Free trial tier - 30 days of Home-level features, then read-only
    /// </summary>
    Free = 0,

    /// <summary>
    /// Organize tier: $3.99/mo - contacts, chores, equipment (including vehicles), todos
    /// </summary>
    Organize = 1,

    /// <summary>
    /// Home tier: $8.99/mo - adds shopping, inventory, recipes
    /// </summary>
    Home = 2,
}
