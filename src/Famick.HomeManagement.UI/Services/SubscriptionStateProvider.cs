using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Subscription;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Blazor implementation of ISubscriptionStateProvider.
/// Reads tier from stored TenantInfoDto after login.
/// Self-hosted deployments return Pro (all features unlocked).
/// </summary>
public class SubscriptionStateProvider : ISubscriptionStateProvider
{
    private readonly IApiClient _apiClient;
    private readonly ITokenStorage _tokenStorage;
    private SubscriptionTier _currentTier = SubscriptionTier.Pro;
    private bool _isTrialActive;
    private bool _isExpired;

    public SubscriptionStateProvider(IApiClient apiClient, ITokenStorage tokenStorage)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
    }

    public SubscriptionTier CurrentTier => _currentTier;
    public bool IsTrialActive => _isTrialActive;
    public bool IsExpired => _isExpired;

    public bool IsFeatureAvailable(string featureArea)
    {
        // During trial, effective tier is Home
        var effectiveTier = _currentTier == SubscriptionTier.Free && _isTrialActive
            ? SubscriptionTier.Home
            : _currentTier;
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

    /// <summary>
    /// Updates subscription state from a TenantInfoDto (called after login).
    /// </summary>
    public void UpdateFromTenant(string? subscriptionTier, bool isTrialActive, bool isExpired)
    {
        if (string.IsNullOrEmpty(subscriptionTier))
        {
            // Self-hosted or unknown — default to Pro (all unlocked)
            _currentTier = SubscriptionTier.Pro;
            _isTrialActive = false;
            _isExpired = false;
            return;
        }

        _currentTier = Enum.TryParse<SubscriptionTier>(subscriptionTier, true, out var tier)
            ? tier
            : SubscriptionTier.Pro;
        _isTrialActive = isTrialActive;
        _isExpired = isExpired;
    }

    public async Task RefreshAsync()
    {
        var token = await _tokenStorage.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token)) return;

        var result = await _apiClient.GetAsync<TenantRefreshDto>("api/v1/tenant");
        if (result.IsSuccess && result.Data != null)
        {
            UpdateFromTenant(result.Data.SubscriptionTier, result.Data.IsTrialActive, result.Data.IsExpired);
        }
    }

    private class TenantRefreshDto
    {
        public string SubscriptionTier { get; set; } = string.Empty;
        public bool IsTrialActive { get; set; }
        public bool IsExpired { get; set; }
    }
}
