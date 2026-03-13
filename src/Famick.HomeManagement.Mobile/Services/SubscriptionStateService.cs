using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Subscription;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Mobile implementation of ISubscriptionStateProvider.
/// Reads tier from Preferences (stored after login).
/// Self-hosted servers return Pro (all features unlocked).
/// </summary>
public class SubscriptionStateService : ISubscriptionStateProvider
{
    private readonly TenantStorage _tenantStorage;
    private readonly ApiSettings _apiSettings;
    private SubscriptionTier? _cachedTier;

    public SubscriptionStateService(TenantStorage tenantStorage, ApiSettings apiSettings)
    {
        _tenantStorage = tenantStorage;
        _apiSettings = apiSettings;
    }

    public SubscriptionTier CurrentTier
    {
        get
        {
            if (_cachedTier.HasValue)
                return _cachedTier.Value;

            // Self-hosted: all features unlocked
            if (_apiSettings.IsSelfHostedServer())
            {
                _cachedTier = SubscriptionTier.Pro;
                return _cachedTier.Value;
            }

            var tierString = _tenantStorage.GetSubscriptionTier();
            _cachedTier = Enum.TryParse<SubscriptionTier>(tierString, true, out var tier)
                ? tier
                : SubscriptionTier.Pro; // Default to Pro if unknown (safe fallback)

            return _cachedTier.Value;
        }
    }

    public bool IsTrialActive
    {
        get
        {
            if (_apiSettings.IsSelfHostedServer()) return false;
            return _tenantStorage.GetIsTrialActive();
        }
    }

    public bool IsExpired
    {
        get
        {
            if (_apiSettings.IsSelfHostedServer()) return false;
            return _tenantStorage.GetIsExpired();
        }
    }

    public bool IsFeatureAvailable(string featureArea)
    {
        // During trial, effective tier is Home
        var effectiveTier = CurrentTier == SubscriptionTier.Free && IsTrialActive
            ? SubscriptionTier.Home
            : CurrentTier;
        return SubscriptionFeatureMap.IsFeatureAvailable(featureArea, effectiveTier);
    }

    public SubscriptionTier GetRequiredTier(string featureArea)
    {
        return SubscriptionFeatureMap.GetRequiredTier(featureArea);
    }

    public string GetFeatureDescription(string featureArea)
    {
        return SubscriptionFeatureMap.GetFeatureDescription(featureArea);
    }

    public Task RefreshAsync()
    {
        // Clear cache so next access re-reads from Preferences
        _cachedTier = null;
        return Task.CompletedTask;
    }
}
