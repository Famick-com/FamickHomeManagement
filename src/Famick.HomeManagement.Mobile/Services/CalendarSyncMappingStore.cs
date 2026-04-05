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
    private const string FileName = "calendarsync.json";
    private readonly string _filePath;
    private CalendarSyncData _data;

    public CalendarSyncMappingStore()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, FileName);
        _data = Load();
    }

    public DateTime? LastSyncedAt => _data.LastSyncedAt;
    public int SyncedCount => _data.Mappings.Count;

    public string? GetDeviceEventId(Guid serverEventId)
    {
        return _data.Mappings.TryGetValue(serverEventId.ToString(), out var entry)
            ? entry.DeviceEventId
            : null;
    }

    public string? GetLastSyncedHash(Guid serverEventId)
    {
        return _data.Mappings.TryGetValue(serverEventId.ToString(), out var entry)
            ? entry.LastSyncedHash
            : null;
    }

    public string? GetLastDeviceHash(Guid serverEventId)
    {
        return _data.Mappings.TryGetValue(serverEventId.ToString(), out var entry)
            ? (string.IsNullOrEmpty(entry.LastDeviceHash) ? null : entry.LastDeviceHash)
            : null;
    }

    public void SetMapping(Guid serverEventId, string deviceEventId, string syncedHash, string deviceHash)
    {
        _data.Mappings[serverEventId.ToString()] = new CalendarSyncEntry
        {
            DeviceEventId = deviceEventId,
            LastSyncedHash = syncedHash,
            LastDeviceHash = deviceHash
        };
    }

    public void RemoveMapping(Guid serverEventId)
    {
        _data.Mappings.Remove(serverEventId.ToString());
    }

    public List<Guid> GetAllSyncedServerEventIds()
    {
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
        _data.LastSyncedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(_filePath, json);
    }

    public void Clear()
    {
        _data = new CalendarSyncData();
        Save();
    }

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

    private CalendarSyncData Load()
    {
        if (!File.Exists(_filePath))
            return new CalendarSyncData();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<CalendarSyncData>(json) ?? new CalendarSyncData();
        }
        catch
        {
            return new CalendarSyncData();
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
