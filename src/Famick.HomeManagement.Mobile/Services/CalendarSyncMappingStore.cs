using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Persists a mapping between server calendar event IDs and device event IDs.
/// Stores hashes to detect changes on both sides for bidirectional sync.
///
/// Hash architecture:
/// - lastSyncedHash: hash of server data pushed to device (detects server-side changes)
/// - lastDeviceHash: hash of device data at last sync (detects device-side edits)
/// Both use the same field set: title + start + end + isAllDay + location + description + recurrenceRule
/// </summary>
public class CalendarSyncMappingStore
{
    private const string LegacyFileName = "calendarsync.json";
    private const string FilePrefix = "calendarsync-";

    private readonly SyncAccountScope _scope;
    private CalendarSyncData _data = new();
    private string? _loadedScope;
    private bool _loaded;

    public CalendarSyncMappingStore(SyncAccountScope scope)
    {
        _scope = scope;
    }

    /// <summary>
    /// Whether an account is signed in, so mappings can be attributed to someone.
    /// Callers that delete device events must check this first — with no scope there is
    /// no way to tell whose mappings are loaded.
    /// </summary>
    public bool HasScope
    {
        get
        {
            EnsureLoaded();
            return _loadedScope != null;
        }
    }

    public DateTime? LastSyncedAt
    {
        get
        {
            EnsureLoaded();
            return _data.LastSyncedAt;
        }
    }

    public int SyncedCount
    {
        get
        {
            EnsureLoaded();
            return _data.Mappings.Count;
        }
    }

    public string? GetDeviceEventId(Guid serverEventId)
    {
        EnsureLoaded();
        return _data.Mappings.TryGetValue(serverEventId.ToString(), out var entry)
            ? entry.DeviceEventId
            : null;
    }

    public string? GetLastSyncedHash(Guid serverEventId)
    {
        EnsureLoaded();
        return _data.Mappings.TryGetValue(serverEventId.ToString(), out var entry)
            ? entry.LastSyncedHash
            : null;
    }

    public string? GetLastDeviceHash(Guid serverEventId)
    {
        EnsureLoaded();
        return _data.Mappings.TryGetValue(serverEventId.ToString(), out var entry)
            ? (string.IsNullOrEmpty(entry.LastDeviceHash) ? null : entry.LastDeviceHash)
            : null;
    }

    public void SetMapping(Guid serverEventId, string deviceEventId, string syncedHash, string deviceHash)
    {
        EnsureLoaded();
        _data.Mappings[serverEventId.ToString()] = new CalendarSyncEntry
        {
            DeviceEventId = deviceEventId,
            LastSyncedHash = syncedHash,
            LastDeviceHash = deviceHash
        };
    }

    public void RemoveMapping(Guid serverEventId)
    {
        EnsureLoaded();
        _data.Mappings.Remove(serverEventId.ToString());
    }

    public List<Guid> GetAllSyncedServerEventIds()
    {
        EnsureLoaded();
        return _data.Mappings.Keys
            .Select(k => Guid.TryParse(k, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    /// <summary>
    /// Returns a dictionary of deviceEventId -> serverEventId for reverse lookups.
    /// </summary>
    public Dictionary<string, Guid> GetDeviceToServerMap()
    {
        EnsureLoaded();
        var map = new Dictionary<string, Guid>();
        foreach (var kvp in _data.Mappings)
        {
            if (Guid.TryParse(kvp.Key, out var serverId))
                map[kvp.Value.DeviceEventId] = serverId;
        }
        return map;
    }

    public void Save()
    {
        EnsureLoaded();
        if (_loadedScope == null) return; // signed out — nothing to attribute these to

        _data.LastSyncedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(PathFor(_loadedScope), json);
    }

    public void Clear()
    {
        EnsureLoaded();
        _data = new CalendarSyncData();
        Save();
    }

    #region Account scoping

    /// <summary>
    /// Loads the mapping set belonging to the signed-in account, reloading when the
    /// account changes. Resolution is deferred rather than done in the constructor
    /// because this is a singleton that outlives any one sign-in.
    /// </summary>
    private void EnsureLoaded()
    {
        var scope = _scope.CurrentKey;
        if (_loaded && scope == _loadedScope) return;

        _loadedScope = scope;
        _loaded = true;
        _data = scope == null ? new CalendarSyncData() : Load(scope);
    }

    private static string PathFor(string scope)
        => Path.Combine(FileSystem.AppDataDirectory, $"{FilePrefix}{scope}.json");

    #endregion

    #region Hashing

    /// <summary>
    /// Computes a hash from a server calendar occurrence for change detection.
    /// </summary>
    public static string ComputeOccurrenceHash(CalendarOccurrence occ)
    {
        var sb = new StringBuilder();
        sb.Append(occ.Title ?? "");
        sb.Append('|');
        sb.Append(occ.StartTimeUtc.ToString("O"));
        sb.Append('|');
        sb.Append(occ.EndTimeUtc.ToString("O"));
        sb.Append('|');
        sb.Append(occ.IsAllDay);
        sb.Append('|');
        sb.Append(occ.Location ?? "");
        sb.Append('|');
        sb.Append(occ.Description ?? "");
        return ComputeSha256(sb.ToString());
    }

    /// <summary>
    /// Computes a hash from a device calendar event for change detection.
    /// </summary>
    public static string ComputeDeviceEventHash(DeviceCalendarEventData evt)
    {
        var sb = new StringBuilder();
        sb.Append(evt.Title ?? "");
        sb.Append('|');
        sb.Append(evt.StartTimeUtc.ToString("O"));
        sb.Append('|');
        sb.Append(evt.EndTimeUtc.ToString("O"));
        sb.Append('|');
        sb.Append(evt.IsAllDay);
        sb.Append('|');
        sb.Append(evt.Location ?? "");
        sb.Append('|');
        sb.Append(evt.Description ?? "");
        return ComputeSha256(sb.ToString());
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    #endregion

    #region Persistence

    private static CalendarSyncData Load(string scope)
    {
        var path = PathFor(scope);

        if (!File.Exists(path))
            AdoptLegacyFile(path);

        if (!File.Exists(path))
            return new CalendarSyncData();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CalendarSyncData>(json) ?? new CalendarSyncData();
        }
        catch
        {
            return new CalendarSyncData();
        }
    }

    /// <summary>
    /// One-time migration from the unscoped mapping file. See the equivalent in
    /// <see cref="ContactSyncMappingStore"/> for why the file is moved and not copied.
    /// </summary>
    private static void AdoptLegacyFile(string scopedPath)
    {
        var legacyPath = Path.Combine(FileSystem.AppDataDirectory, LegacyFileName);
        if (!File.Exists(legacyPath)) return;

        try
        {
            File.Move(legacyPath, scopedPath);
        }
        catch
        {
            // Starting from an empty mapping set is safe — it re-creates events rather
            // than deleting them.
        }
    }

    private class CalendarSyncData
    {
        public DateTime? LastSyncedAt { get; set; }
        public Dictionary<string, CalendarSyncEntry> Mappings { get; set; } = new();
    }

    private class CalendarSyncEntry
    {
        public string DeviceEventId { get; set; } = string.Empty;
        public string LastSyncedHash { get; set; } = string.Empty;
        public string LastDeviceHash { get; set; } = string.Empty;
    }

    #endregion
}
