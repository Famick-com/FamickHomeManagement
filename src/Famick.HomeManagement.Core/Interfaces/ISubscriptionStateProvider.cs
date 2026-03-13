using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Provides subscription state for client-side feature gating.
/// Implemented differently by Blazor (reads from stored TenantInfoDto)
/// and MAUI (reads from Preferences).
/// </summary>
public interface ISubscriptionStateProvider
{
    /// <summary>
    /// The current tenant's subscription tier.
    /// </summary>
    SubscriptionTier CurrentTier { get; }

    /// <summary>
    /// Whether the tenant is on an active free trial.
    /// </summary>
    bool IsTrialActive { get; }

    /// <summary>
    /// Whether the subscription has expired (trial ended or paid subscription lapsed).
    /// </summary>
    bool IsExpired { get; }

    /// <summary>
    /// Checks if a feature area is available at the current subscription tier.
    /// </summary>
    bool IsFeatureAvailable(string featureArea);

    /// <summary>
    /// Gets the minimum tier required for a feature area.
    /// </summary>
    SubscriptionTier GetRequiredTier(string featureArea);

    /// <summary>
    /// Gets a marketing description for a feature area (for upgrade banners).
    /// </summary>
    string GetFeatureDescription(string featureArea);

    /// <summary>
    /// Refreshes subscription state from the server.
    /// </summary>
    Task RefreshAsync();
}
