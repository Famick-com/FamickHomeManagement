using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Persists a mapping between server contact IDs and device contact IDs.
/// Stores a hash of each contact's data to detect changes and avoid unnecessary updates.
/// </summary>
public class ContactSyncMappingStore
{
    private const string FileName = "contactsync.json";
    private readonly string _filePath;
    private ContactSyncData _data;

    public ContactSyncMappingStore()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, FileName);
        _data = Load();
    }

    public DateTime? LastSyncedAt => _data.LastSyncedAt;
    public int SyncedCount => _data.Mappings.Count;

    /// <summary>
    /// Gets the device contact ID for a server contact, or null if not synced.
    /// </summary>
    public string? GetDeviceContactId(Guid serverContactId)
    {
        return _data.Mappings.TryGetValue(serverContactId.ToString(), out var entry)
            ? entry.DeviceContactId
            : null;
    }

    /// <summary>
    /// Gets the last synced hash for a server contact, or null if not synced.
    /// </summary>
    public string? GetLastSyncedHash(Guid serverContactId)
    {
        return _data.Mappings.TryGetValue(serverContactId.ToString(), out var entry)
            ? entry.LastSyncedHash
            : null;
    }

    /// <summary>
    /// Records a mapping between a server contact and a device contact.
    /// </summary>
    public void SetMapping(Guid serverContactId, string deviceContactId, string hash)
    {
        _data.Mappings[serverContactId.ToString()] = new ContactSyncEntry
        {
            DeviceContactId = deviceContactId,
            LastSyncedHash = hash
        };
    }

    /// <summary>
    /// Removes a mapping for a server contact.
    /// </summary>
    public void RemoveMapping(Guid serverContactId)
    {
        _data.Mappings.Remove(serverContactId.ToString());
    }

    /// <summary>
    /// Gets all server contact IDs that have been synced to the device.
    /// </summary>
    public List<Guid> GetAllSyncedServerContactIds()
    {
        return _data.Mappings.Keys
            .Select(k => Guid.TryParse(k, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    /// <summary>
    /// Updates the last sync timestamp and persists all changes to disk.
    /// </summary>
    public void Save()
    {
        _data.LastSyncedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(_filePath, json);
    }

    /// <summary>
    /// Clears all mappings and persists the empty state.
    /// </summary>
    public void Clear()
    {
        _data = new ContactSyncData();
        Save();
    }

    /// <summary>
    /// Computes a hash of the contact's syncable fields to detect changes.
    /// </summary>
    public static string ComputeContactHash(Models.ContactDetailDto contact)
    {
        var sb = new StringBuilder();
        // Hash version: increment to force re-sync of all contacts
        sb.Append("v6|");
        sb.Append(contact.IsGroup);
        sb.Append('|');
        sb.Append(contact.DisplayName);
        sb.Append('|');
        sb.Append(contact.FirstName);
        sb.Append('|');
        sb.Append(contact.MiddleName);
        sb.Append('|');
        sb.Append(contact.LastName);
        sb.Append('|');
        sb.Append(contact.PreferredName);
        sb.Append('|');
        sb.Append(contact.CompanyName);
        sb.Append('|');
        sb.Append(contact.Title);
        sb.Append('|');
        sb.Append(contact.Website);
        sb.Append('|');
        sb.Append(contact.Notes);
        sb.Append('|');
        sb.Append(contact.BirthYear);
        sb.Append('|');
        sb.Append(contact.BirthMonth);
        sb.Append('|');
        sb.Append(contact.BirthDay);

        foreach (var phone in contact.PhoneNumbers.OrderBy(p => p.Id))
            sb.Append($"|P:{phone.PhoneNumber}:{phone.Tag}");

        foreach (var email in contact.EmailAddresses.OrderBy(e => e.Id))
            sb.Append($"|E:{email.Email}:{email.Tag}");

        foreach (var addr in contact.Addresses.OrderBy(a => a.Id))
            sb.Append($"|A:{addr.Address?.AddressLine1}:{addr.Address?.City}:{addr.Address?.StateProvince}:{addr.Address?.PostalCode}:{addr.Address?.Country}:{addr.Tag}");

        foreach (var social in contact.SocialMedia.OrderBy(s => s.Id))
            sb.Append($"|S:{social.Service}:{social.Username}");

        // Photo identifiers (use stable fields, NOT ProfileImageUrl which contains signed tokens)
        sb.Append($"|IMG:{contact.ProfileImageFileName}");
        sb.Append($"|GRAV:{(contact.UseGravatar && contact.GravatarUrl != null ? contact.GravatarUrl : "")}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToBase64String(bytes);
    }

    private ContactSyncData Load()
    {
        if (!File.Exists(_filePath))
            return new ContactSyncData();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<ContactSyncData>(json) ?? new ContactSyncData();
        }
        catch
        {
            return new ContactSyncData();
        }
    }

    private class ContactSyncData
    {
        public DateTime? LastSyncedAt { get; set; }
        public Dictionary<string, ContactSyncEntry> Mappings { get; set; } = new();
    }

    private class ContactSyncEntry
    {
        public string DeviceContactId { get; set; } = string.Empty;
        public string LastSyncedHash { get; set; } = string.Empty;
    }
}
