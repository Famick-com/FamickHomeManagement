using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Subscription;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Mobile.Messages;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// Resolves a scoped <see cref="ShoppingApiClient"/> per refresh.
    /// </summary>
    /// <remarks>
    /// The factory rather than the client itself: this service is a singleton and the API
    /// client is scoped, so holding one would capture a scope for the lifetime of the app.
    /// </remarks>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// One refresh at a time. Resuming the app, opening the plans page and polling after a
    /// purchase can all land together, and they would otherwise race to write the same
    /// three preference keys.
    /// </summary>
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private SubscriptionTier? _cachedTier;

    public SubscriptionStateService(
        TenantStorage tenantStorage,
        ApiSettings apiSettings,
        IServiceScopeFactory scopeFactory)
    {
        _tenantStorage = tenantStorage;
        _apiSettings = apiSettings;
        _scopeFactory = scopeFactory;
    }

    public SubscriptionTier CurrentTier
    {
        get
        {
            // Read once into a local. RefreshAsync sets this to null, and Nullable<T> is
            // two fields rather than one atomic value — so testing HasValue and then
            // reading Value can throw, or see a torn pair, if the two ever run on
            // different threads.
            var cached = _cachedTier;
            if (cached.HasValue)
                return cached.Value;

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

    /// <summary>
    /// Re-reads the household's entitlement from the server.
    /// </summary>
    /// <remarks>
    /// This used to only drop the local cache, which could not change the answer: the
    /// values behind it are written once, at login, and nothing else ever wrote them. So a
    /// tier that changed while the app was running — someone upgrading on another device,
    /// or a purchase on this one landing through the store's webhook — could not be seen
    /// until the next sign-in.
    ///
    /// <para>Reads <c>api/v1/tenant</c>, which stays reachable when a subscription has
    /// lapsed. That matters: the household most likely to be buying is the one currently
    /// being refused writes.</para>
    ///
    /// <para>A failed call leaves the cached state alone. Downgrading someone because their
    /// phone lost signal would lock them out of features they have paid for.</para>
    /// </remarks>
    public async Task RefreshAsync()
    {
        // Self-hosted and proxied households have no subscription to read, and no cloud
        // endpoint to read it from.
        if (_apiSettings.IsSelfHostedServer()) return;

        await _refreshLock.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var apiClient = scope.ServiceProvider.GetRequiredService<ShoppingApiClient>();

            var result = await apiClient.GetTenantAsync();

            if (!result.Success || result.Data is null) return;

            var previousTier = _tenantStorage.GetSubscriptionTier();
            var previousTrial = _tenantStorage.GetIsTrialActive();
            var previousExpired = _tenantStorage.GetIsExpired();

            _tenantStorage.SetSubscriptionState(
                result.Data.SubscriptionTier,
                result.Data.IsTrialActive,
                result.Data.IsExpired);

            _cachedTier = null;

            var changed =
                !string.Equals(previousTier, result.Data.SubscriptionTier, StringComparison.OrdinalIgnoreCase)
                || previousTrial != result.Data.IsTrialActive
                || previousExpired != result.Data.IsExpired;

            if (changed)
            {
                WeakReferenceMessenger.Default.Send(
                    new SubscriptionStateChangedMessage(result.Data.SubscriptionTier ?? string.Empty));
            }
        }
        catch
        {
            // Keep whatever was cached. See the remarks above.
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
