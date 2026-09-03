using Famick.HomeManagement.Core.Platform;
using Microsoft.AspNetCore.Components;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Client-side cache of the server's <see cref="ServerPlatform"/>. Resolved once
/// from the anonymous <c>/api/setup/status</c> boot endpoint and reused across
/// pages so the UI can adapt without re-fetching or re-deriving config flags.
/// Registered scoped — in Blazor WebAssembly that lives for the app's lifetime.
/// </summary>
public class PlatformState
{
    private readonly IApiClient _apiClient;
    private ServerPlatform? _platform;

    public PlatformState(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Primes the cache from a <c>SetupStatusResponse</c> already fetched at boot,
    /// avoiding a second round-trip.
    /// </summary>
    public void Set(ServerPlatform platform) => _platform = platform;

    /// <summary>
    /// Returns the platform, fetching it once if not already cached. Falls back to
    /// <see cref="ServerPlatform.SelfHosted"/> if the status call fails.
    /// </summary>
    public async Task<ServerPlatform> GetAsync()
    {
        if (_platform is { } cached)
        {
            return cached;
        }

        var result = await _apiClient.GetSetupStatusAsync();
        _platform = result.IsSuccess && result.Data is not null
            ? result.Data.Platform
            : ServerPlatform.SelfHosted;
        return _platform.Value;
    }

    /// <summary>
    /// Guard for pages that configure the server process (plugin registry,
    /// server-config overlay, mobile-app setup). On a multi-tenant server those
    /// pages have no owner, so the route renders the router's not-found content
    /// instead. Returns <c>true</c> when it did, so the caller can stop
    /// initialising.
    /// </summary>
    public async Task<bool> NotFoundIfMultiTenantAsync(NavigationManager navigation)
    {
        if (await GetAsync() != ServerPlatform.Cloud)
        {
            return false;
        }

        navigation.NotFound();
        return true;
    }
}
