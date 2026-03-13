using System.Text.Json;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Shared.Contacts;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Persists a mapping between server contact IDs and device contact IDs.
/// Stores a hash of each contact's data to detect changes and avoid unnecessary updates.
///
/// Hash architecture:
/// All hashing is delegated to ContactHasher (in Famick.HomeManagement.Shared).
/// Server data is first converted to DeviceContactData via MapServerToDeviceData(),
/// which normalizes fields to their device representation (tag round-trips, nickname
/// mapping, etc.). This eliminates hash mismatches between server and device.
///
/// Two hash variants exist:
/// - ComputeContactHash: includes photo fields (for server→device change detection)
/// - ComputeDeviceContactHash / ComputeContactFieldsHash: excludes photos (for device→server)
/// Both share the same base string via ContactHasher.BuildDeviceFieldsString.
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

    public string? GetDeviceContactId(Guid serverContactId)
    {
        return _data.Mappings.TryGetValue(serverContactId.ToString(), out var entry)
            ? entry.DeviceContactId
            : null;
    }

    public string? GetLastSyncedHash(Guid serverContactId)
    {
        return _data.Mappings.TryGetValue(serverContactId.ToString(), out var entry)
            ? entry.LastSyncedHash
            : null;
    }

    public string? GetLastDeviceFieldsHash(Guid serverContactId)
    {
        return _data.Mappings.TryGetValue(serverContactId.ToString(), out var entry)
            ? (string.IsNullOrEmpty(entry.LastDeviceFieldsHash) ? null : entry.LastDeviceFieldsHash)
            : null;
    }

    public void SetMapping(Guid serverContactId, string deviceContactId, string hash, string deviceFieldsHash)
    {
        _data.Mappings[serverContactId.ToString()] = new ContactSyncEntry
        {
            DeviceContactId = deviceContactId,
            LastSyncedHash = hash,
            LastDeviceFieldsHash = deviceFieldsHash
        };
    }

    public void RemoveMapping(Guid serverContactId)
    {
        _data.Mappings.Remove(serverContactId.ToString());
    }

    public List<Guid> GetAllSyncedServerContactIds()
    {
        return _data.Mappings.Keys
            .Select(k => Guid.TryParse(k, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    public void Save()
    {
        _data.LastSyncedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(_filePath, json);
    }

    public void Clear()
    {
        _data = new ContactSyncData();
        Save();
    }

    #region Hashing — Delegates to ContactHasher

    /// <summary>
    /// Computes a hash for server→device change detection. Includes photo fields.
    /// Converts server data to device representation first, ensuring hash alignment.
    /// </summary>
    public static string ComputeContactHash(ContactDetailDto contact)
    {
        var deviceData = MapServerToDeviceData(contact);
        return ContactHasher.ComputeHashWithPhotos(
            deviceData,
            contact.ProfileImageFileName,
            contact.UseGravatar,
            contact.GravatarUrl);
    }

    /// <summary>
    /// Computes a baseline hash for device→server change detection.
    /// Converts server data to device representation, then hashes without photos.
    /// The result matches what ComputeDeviceContactHash produces for the same data
    /// read back from the device.
    /// </summary>
    public static string ComputeContactFieldsHash(ContactDetailDto contact)
    {
        var deviceData = MapServerToDeviceData(contact);
        return ContactHasher.ComputeHash(deviceData);
    }

    /// <summary>
    /// Computes a hash from device contact data (excludes photo fields).
    /// Delegates to ContactHasher.ComputeHash.
    /// </summary>
    public static string ComputeDeviceContactHash(DeviceContactData device)
    {
        return ContactHasher.ComputeHash(device);
    }

    #endregion

    #region Server → Device Data Mapping

    /// <summary>
    /// Converts server contact data to a DeviceContactData representing what the device
    /// would look like after syncing. This accounts for all field mapping differences:
    /// - DisplayName: computed field, null for non-groups
    /// - PreferredName → Nickname (only when different from FirstName)
    /// - CompanyName → OrganizationName, Title → JobTitle
    /// - Tag values normalized through device round-trip (e.g., School→Other→99)
    /// - Social media service normalized (unknown services → 0)
    /// - Notes always null (iOS can't read them back)
    /// </summary>
    public static DeviceContactData MapServerToDeviceData(ContactDetailDto contact)
    {
        var data = new DeviceContactData
        {
            IsGroup = contact.IsGroup,
            Website = contact.Website,
            Notes = null, // Always null — iOS can't read notes, excluded from hash
            BirthYear = contact.BirthYear,
            BirthMonth = contact.BirthMonth,
            BirthDay = contact.BirthDay
        };

        if (contact.IsGroup)
        {
            data.DisplayName = contact.DisplayName ?? contact.CompanyName;
            data.OrganizationName = contact.DisplayName ?? contact.CompanyName;
        }
        else
        {
            data.FirstName = contact.FirstName;
            data.MiddleName = contact.MiddleName;
            data.LastName = contact.LastName;
            data.OrganizationName = contact.CompanyName;
            data.JobTitle = contact.Title;

            // Nickname is only set when PreferredName differs from FirstName
            // (matches the logic in iOS/Android MapContactFields)
            if (!string.IsNullOrWhiteSpace(contact.PreferredName) && contact.PreferredName != contact.FirstName)
                data.Nickname = contact.PreferredName;
        }

        foreach (var p in contact.PhoneNumbers)
        {
            data.PhoneNumbers.Add(new DevicePhoneEntry
            {
                PhoneNumber = p.PhoneNumber,
                Tag = ContactHasher.NormalizePhoneTag(p.Tag)
            });
        }

        foreach (var e in contact.EmailAddresses)
        {
            data.EmailAddresses.Add(new DeviceEmailEntry
            {
                Email = e.Email,
                Tag = ContactHasher.NormalizeEmailTag(e.Tag)
            });
        }

        foreach (var a in contact.Addresses.Where(a => a.Address != null))
        {
            data.Addresses.Add(new DeviceAddressEntry
            {
                AddressLine1 = a.Address!.AddressLine1,
                City = a.Address.City,
                StateProvince = a.Address.StateProvince,
                PostalCode = a.Address.PostalCode,
                Country = a.Address.Country,
                Tag = ContactHasher.NormalizeAddressTag(a.Tag)
            });
        }

        foreach (var s in contact.SocialMedia)
        {
            data.SocialProfiles.Add(new DeviceSocialEntry
            {
                Service = ContactHasher.NormalizeSocialService(s.Service),
                Username = s.Username,
                ProfileUrl = s.ProfileUrl
            });
        }

        return data;
    }

    #endregion

    #region Persistence

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
        public string LastDeviceFieldsHash { get; set; } = string.Empty;
    }

    #endregion
}
