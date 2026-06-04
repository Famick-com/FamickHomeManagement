using System.Text.Json;
using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Per-install cache of <c>email → home server</c> results returned by
/// <see cref="EmailLookupApi"/>. Once a user signs in successfully on
/// this device with a given email, subsequent sign-ins for the same
/// email skip the AuthProxy lookup round-trip entirely — they go
/// straight into the existing <c>/check</c> + password / passkey /
/// OAuth flow against the cached proxied <c>BaseUrl</c>.
///
/// Persisted as a single JSON-encoded dictionary in MAUI
/// <see cref="Preferences"/>. Per-entry TTL caps stale entries at 30
/// days; manual invalidation is exposed via <see cref="Invalidate"/>
/// for the "use a different home server" flow and for the
/// <c>home_server_offline</c> recovery path.
/// </summary>
public sealed class ProxiedEmailCache
{
    private const string PreferenceKey = "proxied_email_cache_v1";
    private const string LastEmailKey = "proxied_last_email";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object _gate = new();

    public bool TryGet(string email, out EmailLookupSuccess hit)
    {
        var key = Normalize(email);
        hit = default!;
        if (string.IsNullOrEmpty(key)) return false;

        var map = Load();
        if (!map.TryGetValue(key, out var entry)) return false;

        if (DateTimeOffset.UtcNow - entry.CachedAt > DefaultTtl)
        {
            // Stale → drop it now so we don't keep re-checking the TTL
            // every login.
            Invalidate(email);
            return false;
        }

        hit = new EmailLookupSuccess
        {
            Email = key,
            HomeServerId = entry.HomeServerId,
            DisplayName = entry.DisplayName,
            BaseUrl = entry.BaseUrl,
        };
        return true;
    }

    public void Set(EmailLookupSuccess result)
    {
        var key = Normalize(result.Email);
        if (string.IsNullOrEmpty(key)) return;

        lock (_gate)
        {
            var map = Load();
            map[key] = new CacheEntry
            {
                HomeServerId = result.HomeServerId,
                DisplayName = result.DisplayName,
                BaseUrl = result.BaseUrl,
                CachedAt = DateTimeOffset.UtcNow,
            };
            Save(map);
        }
        Preferences.Default.Set(LastEmailKey, key);
    }

    public void Invalidate(string email)
    {
        var key = Normalize(email);
        if (string.IsNullOrEmpty(key)) return;

        lock (_gate)
        {
            var map = Load();
            if (map.Remove(key))
            {
                Save(map);
            }
        }

        // If the invalidated email is what we'd pre-fill, forget the
        // pre-fill too — the user almost certainly wants a fresh start.
        if (Preferences.Default.Get(LastEmailKey, string.Empty) == key)
        {
            Preferences.Default.Remove(LastEmailKey);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Preferences.Default.Remove(PreferenceKey);
            Preferences.Default.Remove(LastEmailKey);
        }
    }

    /// <summary>
    /// Returns the email most recently saved via <see cref="Set"/>, or
    /// null if the cache is empty. Used to pre-fill the email entry on
    /// the sign-in page so a repeat user gets one-tap Continue.
    /// </summary>
    public string? LastUsedEmail
    {
        get
        {
            var v = Preferences.Default.Get(LastEmailKey, string.Empty);
            return string.IsNullOrEmpty(v) ? null : v;
        }
    }

    private static string Normalize(string? email) =>
        string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();

    private static Dictionary<string, CacheEntry> Load()
    {
        var raw = Preferences.Default.Get(PreferenceKey, string.Empty);
        if (string.IsNullOrEmpty(raw)) return new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(raw, JsonOptions)
                ?? new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // Corrupted preference (schema change, partial write) — drop it.
            Preferences.Default.Remove(PreferenceKey);
            return new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        }
    }

    private static void Save(Dictionary<string, CacheEntry> map)
    {
        var json = JsonSerializer.Serialize(map, JsonOptions);
        Preferences.Default.Set(PreferenceKey, json);
    }

    private sealed class CacheEntry
    {
        public Guid HomeServerId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public DateTimeOffset CachedAt { get; set; }
    }
}
