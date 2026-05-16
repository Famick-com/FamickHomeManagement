using System.Collections.Concurrent;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Phase 4 chunk 4.H — short-timeout reachability probe against a
/// self-hosted server's <c>/health</c> endpoint. Used by
/// <see cref="ApiSettings"/> on a self-hosted login to pick between LAN
/// (direct) and proxy.famick.com (off-network).
///
/// Hard 500 ms timeout per the design-doc recommendation; longer timeouts
/// hurt UX when the home Wi-Fi is unreachable, shorter timeouts trip false
/// negatives on slow LAN paths. Phase 4 ships the probe but the proxy
/// fallback URL is still Phase 8 work — the BaseUrl fall-through routes to
/// the self-hosted URL regardless today; ApiSettings (4.H) flips that
/// behavior in once proxy.famick.com is live.
///
/// Negative-result cache: <see cref="NegativeCacheDuration"/> sliding TTL
/// per URL so we don't pummel an unreachable LAN endpoint on every app
/// foreground. Positive results retry sooner (no cache) to recover from
/// transient blips.
/// </summary>
public class LocalServerProbeService
{
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan NegativeCacheDuration = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, DateTime> _negativeCache = new();

    public async Task<bool> IsReachableAsync(string localServerUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localServerUrl))
            return false;

        if (_negativeCache.TryGetValue(localServerUrl, out var probedAt))
        {
            if (DateTime.UtcNow - probedAt < NegativeCacheDuration)
                return false;

            _negativeCache.TryRemove(localServerUrl, out _);
        }

        var healthUrl = localServerUrl.TrimEnd('/') + "/health";
        using var http = new HttpClient { Timeout = ProbeTimeout };

        try
        {
            using var response = await http.GetAsync(healthUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
                return true;
        }
        catch (TaskCanceledException)
        {
            // Timeout — most common failure on LAN-unreachable paths.
        }
        catch (HttpRequestException)
        {
            // Connection refused / DNS / TLS issue — also a failure.
        }

        _negativeCache[localServerUrl] = DateTime.UtcNow;
        return false;
    }

    /// <summary>Test-only: force the next probe to skip the cache.</summary>
    public void InvalidateCache(string localServerUrl)
        => _negativeCache.TryRemove(localServerUrl, out _);
}
