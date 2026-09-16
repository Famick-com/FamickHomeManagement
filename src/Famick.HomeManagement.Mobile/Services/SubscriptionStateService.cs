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

    /// <summary>
    /// Guards the cached tier and the preference values it is derived from.
    /// </summary>
    /// <remarks>
    /// Separate from the refresh lock, which serialises network calls. This one is held
    /// only long enough to read or publish the cache, so that a reader cannot fill it from
    /// preferences that a refresh is midway through replacing — which would leave a stale
    /// tier cached with nothing left to invalidate it.
    /// </remarks>
    private readonly object _cacheGate = new();

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
            // Read and fill under the same gate a refresh publishes through. Otherwise a
            // reader can take the old preference values, be overtaken by a refresh that
            // writes new ones and clears the cache, and then store what it read — leaving a
            // stale tier cached with nothing left to invalidate it.
            //
            // Every return is from a local, never from the field: Nullable<T> is two fields
            // rather than one atomic value, so reading .Value after a separate .HasValue
            // check can throw if the two land either side of a clear.
            lock (_cacheGate)
            {
                return ReadTierUnderGate();
            }
        }
    }

    /// <remarks>
    /// Read under the same gate as the tier. A refresh publishes all three together, and
    /// reading one outside the gate can catch the set half-replaced — a new tier beside a
    /// stale trial flag, which is the pair that decides what a household may open.
    /// </remarks>
    public bool IsTrialActive
    {
        get
        {
            if (_apiSettings.IsSelfHostedServer()) return false;

            lock (_cacheGate)
            {
                return _tenantStorage.GetIsTrialActive();
            }
        }
    }

    /// <inheritdoc cref="IsTrialActive"/>
    public bool IsExpired
    {
        get
        {
            if (_apiSettings.IsSelfHostedServer()) return false;

            lock (_cacheGate)
            {
                return _tenantStorage.GetIsExpired();
            }
        }
    }

    /// <summary>Reads the cached tier, filling it if empty. Caller holds the gate.</summary>
    private SubscriptionTier ReadTierUnderGate()
    {
        var cached = _cachedTier;
        if (cached.HasValue)
            return cached.Value;

        // Self-hosted: all features unlocked
        if (_apiSettings.IsSelfHostedServer())
        {
            _cachedTier = SubscriptionTier.Pro;
            return SubscriptionTier.Pro;
        }

        var tierString = _tenantStorage.GetSubscriptionTier();
        var resolved = Enum.TryParse<SubscriptionTier>(tierString, true, out var tier)
            ? tier
            : SubscriptionTier.Pro; // Default to Pro if unknown (safe fallback)

        _cachedTier = resolved;
        return resolved;
    }

    /// <summary>
    /// Tier and trial state as they were at one instant.
    /// </summary>
    /// <remarks>
    /// Any decision that weighs more than one of these has to read them together. Gating
    /// each property separately makes every read atomic but leaves the decision spanning
    /// several of them, so a refresh landing mid-way produces a combination that never
    /// actually existed — a tier from before it beside a trial flag from after.
    /// </remarks>
    private (SubscriptionTier Tier, bool IsTrialActive) SnapshotUnderGate()
    {
        if (_apiSettings.IsSelfHostedServer()) return (SubscriptionTier.Pro, false);

        lock (_cacheGate)
        {
            return (ReadTierUnderGate(), _tenantStorage.GetIsTrialActive());
        }
    }

    public bool IsFeatureAvailable(string featureArea)
    {
        // One snapshot, not three reads. Read separately, a refresh can arrive between them
        // and produce a pairing that never held: the tier read before it, deciding the
        // trial no longer applies, and the tier read after it saying Free — which denies a
        // feature the trial was paying for.
        var (tier, isTrialActive) = SnapshotUnderGate();

        // During trial, effective tier is Home
        var effectiveTier = tier == SubscriptionTier.Free && isTrialActive
            ? SubscriptionTier.Home
            : tier;

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

            // Published together, so a reader sees either the old values with the old
            // cache, or the new values with an empty one — never new values behind a cache
            // that is about to be filled from the old ones.
            lock (_cacheGate)
            {
                _tenantStorage.SetSubscriptionState(
                    result.Data.SubscriptionTier,
                    result.Data.IsTrialActive,
                    result.Data.IsExpired);

                _cachedTier = null;
            }

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
