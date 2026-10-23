using Famick.HomeManagement.Core.DTOs.Tenant;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Core.Messaging.Messages;
using Famick.HomeManagement.Core.Subscription;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Blazor implementation of ISubscriptionStateProvider.
/// Reads tier from stored TenantInfoDto after login, and from the server when the app starts.
/// Self-hosted servers report themselves unrestricted, which unlocks every feature.
/// </summary>
public class SubscriptionStateProvider : ISubscriptionStateProvider
{
    private readonly IApiClient _apiClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly IMessageBus _messageBus;

    /// <summary>
    /// What a household is treated as when its tier is missing or unreadable.
    /// </summary>
    /// <remarks>
    /// The lowest paid tier. The server enforces the real tier on every request, so this only
    /// decides what the UI offers until the next refresh.
    /// </remarks>
    private const SubscriptionTier UnknownTierFallback = SubscriptionTier.Organize;

    private SubscriptionTier _currentTier = UnknownTierFallback;
    private bool _isTrialActive;
    private bool _isExpired;
    private bool _isUnrestricted;

    public SubscriptionStateProvider(IApiClient apiClient, ITokenStorage tokenStorage, IMessageBus messageBus)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _messageBus = messageBus;
    }

    public SubscriptionTier CurrentTier => _currentTier;
    public bool IsTrialActive => _isTrialActive;
    public bool IsExpired => _isExpired;
    public bool IsUnrestricted => _isUnrestricted;

    public bool IsFeatureAvailable(string featureArea)
    {
        if (_isUnrestricted) return true;

        var effectiveTier = SubscriptionFeatureMap.GetEffectiveTier(_currentTier, _isTrialActive);
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
    /// Updates subscription state from the household the server reported (after login, and on
    /// <see cref="RefreshAsync"/>).
    /// </summary>
    /// <remarks>
    /// Only <paramref name="isUnrestricted"/> unlocks everything. A tier that is missing or
    /// unreadable resolves to <see cref="UnknownTierFallback"/>.
    /// </remarks>
    public void UpdateFromTenant(string? subscriptionTier, bool isTrialActive, bool isExpired, bool isUnrestricted)
    {
        _isUnrestricted = isUnrestricted;

        if (isUnrestricted)
        {
            _currentTier = SubscriptionTier.Home;
            _isTrialActive = false;
            _isExpired = false;
        }
        else
        {
            _currentTier = ParseTier(subscriptionTier);
            _isTrialActive = isTrialActive;
            _isExpired = isExpired;
        }

        _messageBus.Publish(new SubscriptionStateChangedMessage(_currentTier.ToString()) { Source = "blazor" });
    }

    /// <remarks>
    /// IsDefined as well as TryParse: TryParse also accepts numeric text, so "7" would parse to
    /// a tier that does not exist and compare above every real one.
    /// </remarks>
    private static SubscriptionTier ParseTier(string? subscriptionTier) =>
        Enum.TryParse<SubscriptionTier>(subscriptionTier, ignoreCase: true, out var tier) && Enum.IsDefined(tier)
            ? tier
            : UnknownTierFallback;

    public async Task RefreshAsync()
    {
        // The access token is held in memory, so after a page load there is none yet. The
        // session itself is the refresh cookie: restore it first, or there is nothing to send.
        // Asking the request below to recover from its own 401 instead would announce an
        // expired session to a visitor who simply is not signed in.
        var token = await _tokenStorage.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token) && !await _apiClient.RefreshSessionAsync()) return;

        var result = await _apiClient.GetAsync<TenantDto>("api/v1/tenant");
        if (result.IsSuccess && result.Data != null)
        {
            UpdateFromTenant(
                result.Data.SubscriptionTier,
                result.Data.IsTrialActive,
                result.Data.IsExpired,
                result.Data.IsUnrestricted);
        }
    }
}
