using System.Globalization;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Shared.Contacts;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Coordinates the full contact sync flow: fetch from server, sync to device, update mappings.
/// </summary>
public class ContactSyncOrchestrator
{
    private readonly IContactSyncService _syncService;
    private readonly ShoppingApiClient _apiClient;
    private readonly ContactSyncMappingStore _mappingStore;

    public ContactSyncOrchestrator(
        IContactSyncService syncService,
        ShoppingApiClient apiClient,
        ContactSyncMappingStore mappingStore)
    {
        _syncService = syncService;
        _apiClient = apiClient;
        _mappingStore = mappingStore;
    }

    /// <summary>
    /// Runs a full sync: pushes device edits to server, then fetches all contacts and syncs to device.
    /// </summary>
    public async Task<ContactSyncResult> SyncAsync(CancellationToken ct = default)
    {
        // Check permission
        var hasPermission = await _syncService.HasPermissionAsync();
        if (!hasPermission)
        {
            return ContactSyncResult.Fail("Contact permission not granted");
        }

        // Phase 0: Push device edits to server before pulling server state
        await PushDeviceEditsToServerAsync(ct);

        // Fetch all contact summaries from server (all pages)
        var allSummaries = new List<ContactSummaryDto>();
        var page = 1;
        const int pageSize = 100;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var filter = new ContactFilterRequest
            {
                IsActive = true,
                Page = page,
                PageSize = pageSize
            };

            var result = await _apiClient.GetContactsAsync(filter);
            if (!result.Success || result.Data == null)
            {
                return ContactSyncResult.Fail(result.ErrorMessage ?? "Failed to fetch contacts from server");
            }

            allSummaries.AddRange(result.Data.Items);

            if (!result.Data.HasNextPage)
                break;

            page++;
        }

        // Fetch full details for each contact (needed for addresses, phones, emails, etc.)
        var allContacts = new List<ContactDetailDto>();
        foreach (var summary in allSummaries)
        {
            ct.ThrowIfCancellationRequested();

            var detailResult = await _apiClient.GetContactAsync(summary.Id);
            if (detailResult.Success && detailResult.Data != null)
            {
                allContacts.Add(detailResult.Data);
            }
        }

        // Download photos for all contacts that have a photo URL.
        // Not gated by hash — photos must be re-downloaded because they are transient
        // (not included in the hash) and a silent download failure should not prevent retry.
        foreach (var contact in allContacts)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Prefer uploaded profile image; fall back to Gravatar if enabled.
                // For Gravatar, download directly (bypass API client) since it's an
                // external URL that doesn't need auth and uses d=404 to signal "no image".
                var profileImageUrl = contact.ProfileImageUrl;
                var gravatarUrl = contact.UseGravatar ? contact.GravatarUrl : null;

                byte[]? photoBytes = null;

                if (!string.IsNullOrEmpty(profileImageUrl))
                {
                    photoBytes = await _apiClient.DownloadBytesAsync(profileImageUrl);
                }
                else if (!string.IsNullOrEmpty(gravatarUrl))
                {
                    photoBytes = await DownloadGravatarAsync(gravatarUrl);
                }

                // Validate that downloaded bytes are actually an image (JPEG, PNG, GIF, WebP)
                if (photoBytes is { Length: > 0 } && IsValidImage(photoBytes))
                    contact.PhotoData = photoBytes;
                else
                    contact.PhotoData = null;
            }
            catch
            {
                contact.PhotoData = null;
            }
        }

        // Sync to device
        var syncResult = await _syncService.SyncContactsAsync(allContacts, ct);

        // Update preference for last sync
        if (syncResult.Success)
        {
            Preferences.Set("ContactSyncLastSyncedAt", DateTime.UtcNow.ToString("O"));
            Preferences.Set("ContactSyncCount", _mappingStore.SyncedCount);
        }

        return syncResult;
    }

    /// <summary>
    /// Syncs a single contact to the device immediately (e.g. after create/edit).
    /// Runs fire-and-forget — failures are logged but not surfaced to the user.
    /// </summary>
    public async Task SyncSingleContactAsync(Guid contactId)
    {
        try
        {
            if (!IsSyncEnabled) return;

            var hasPermission = await _syncService.HasPermissionAsync();
            if (!hasPermission) return;

            var detailResult = await _apiClient.GetContactAsync(contactId);
            if (!detailResult.Success || detailResult.Data == null) return;

            var contact = detailResult.Data;

            // Download photo
            try
            {
                var profileImageUrl = contact.ProfileImageUrl;
                var gravatarUrl = contact.UseGravatar ? contact.GravatarUrl : null;

                byte[]? photoBytes = null;

                if (!string.IsNullOrEmpty(profileImageUrl))
                    photoBytes = await _apiClient.DownloadBytesAsync(profileImageUrl);
                else if (!string.IsNullOrEmpty(gravatarUrl))
                    photoBytes = await DownloadGravatarAsync(gravatarUrl);

                if (photoBytes is { Length: > 0 } && IsValidImage(photoBytes))
                    contact.PhotoData = photoBytes;
            }
            catch
            {
                contact.PhotoData = null;
            }

            await _syncService.SyncSingleContactToDeviceAsync(contact);
        }
        catch { /* Non-critical */ }
    }

    /// <summary>
    /// Deletes a single contact from the device (e.g. when server notifies deletion via push).
    /// </summary>
    public async Task DeleteSingleContactAsync(Guid contactId)
    {
        try
        {
            if (!IsSyncEnabled) return;
            var hasPermission = await _syncService.HasPermissionAsync();
            if (!hasPermission) return;
            await _syncService.DeleteSingleContactFromDeviceAsync(contactId);
        }
        catch { /* Non-critical */ }
    }

    /// <summary>
    /// Removes all Famick-synced contacts from the device and clears mappings.
    /// </summary>
    public async Task<ContactSyncResult> RemoveAllAsync(CancellationToken ct = default)
    {
        var result = await _syncService.RemoveAllSyncedContactsAsync(ct);

        if (result.Success)
        {
            Preferences.Remove("ContactSyncLastSyncedAt");
            Preferences.Set("ContactSyncCount", 0);
        }

        return result;
    }

    /// <summary>
    /// Gets the current sync status.
    /// </summary>
    public async Task<ContactSyncStatus> GetStatusAsync()
    {
        return await _syncService.GetSyncStatusAsync();
    }

    /// <summary>
    /// Whether contact sync is enabled by the user.
    /// </summary>
    public static bool IsSyncEnabled
    {
        get => Preferences.Get("ContactSyncEnabled", false);
        set => Preferences.Set("ContactSyncEnabled", value);
    }

    /// <summary>
    /// When the last successful sync completed.
    /// </summary>
    public static DateTime? LastSyncedAt
    {
        get
        {
            var str = Preferences.Get("ContactSyncLastSyncedAt", null as string);
            return str != null ? DateTime.Parse(str, null, DateTimeStyles.RoundtripKind) : null;
        }
    }

    /// <summary>
    /// Whether a sync should run based on the minimum interval and enabled state.
    /// </summary>
    public static bool ShouldSync(TimeSpan minInterval)
    {
        if (!IsSyncEnabled) return false;
        var last = LastSyncedAt;
        return last == null || DateTime.UtcNow - last.Value > minInterval;
    }

    /// <summary>
    /// Pushes device-side edits to the server before the normal server→device sync.
    /// For each mapped contact, reads the device contact, computes a hash, and compares
    /// to the stored baseline. If only the device changed, pushes to server.
    /// If both sides changed, server wins (skip push).
    /// </summary>
    private async Task PushDeviceEditsToServerAsync(CancellationToken ct)
    {
        var syncedIds = _mappingStore.GetAllSyncedServerContactIds();

        foreach (var serverContactId in syncedIds)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var deviceId = _mappingStore.GetDeviceContactId(serverContactId);
                var lastDeviceHash = _mappingStore.GetLastDeviceFieldsHash(serverContactId);
                if (deviceId == null || string.IsNullOrEmpty(lastDeviceHash)) continue;

                // Read current device state
                var deviceContact = await _syncService.ReadDeviceContactAsync(deviceId);
                if (deviceContact == null) continue; // deleted on device, don't propagate

                // Check if device data changed
                var currentDeviceHash = ContactSyncMappingStore.ComputeDeviceContactHash(deviceContact);
                if (currentDeviceHash == lastDeviceHash) continue; // no change

                // Device was edited. Check if server also changed (server-wins conflict).
                var serverResult = await _apiClient.GetContactAsync(serverContactId);
                if (!serverResult.Success || serverResult.Data == null) continue;

                var serverFieldsHash = ContactSyncMappingStore.ComputeContactFieldsHash(serverResult.Data);
                if (serverFieldsHash != lastDeviceHash)
                {
                    // Both sides changed → server wins, skip push
                    continue;
                }

                // Only device changed → push to server
                var request = MapDeviceContactToSyncRequest(deviceContact);
                await _apiClient.DeviceSyncUpdateContactAsync(serverContactId, request);
            }
            catch
            {
                // Non-critical — continue with next contact
            }
        }
    }

    /// <summary>
    /// Maps a DeviceContactData to a DeviceSyncUpdateRequest for the API.
    /// </summary>
    private static DeviceSyncUpdateRequest MapDeviceContactToSyncRequest(DeviceContactData device)
    {
        var request = new DeviceSyncUpdateRequest
        {
            FirstName = device.IsGroup ? null : device.FirstName,
            MiddleName = device.IsGroup ? null : device.MiddleName,
            LastName = device.IsGroup ? null : device.LastName,
            PreferredName = device.Nickname,
            CompanyName = device.IsGroup ? device.DisplayName ?? device.OrganizationName : device.OrganizationName,
            Title = device.JobTitle,
            Website = device.Website,
            Notes = device.Notes, // null on iOS (entitlement not provisioned), server preserves existing
            BirthYear = device.BirthYear,
            BirthMonth = device.BirthMonth,
            BirthDay = device.BirthDay
        };

        foreach (var p in device.PhoneNumbers)
        {
            request.PhoneNumbers.Add(new DeviceSyncPhoneEntry
            {
                PhoneNumber = p.PhoneNumber,
                Tag = p.Tag
            });
        }

        foreach (var e in device.EmailAddresses)
        {
            request.EmailAddresses.Add(new DeviceSyncEmailEntry
            {
                Email = e.Email,
                Tag = e.Tag
            });
        }

        foreach (var a in device.Addresses)
        {
            request.Addresses.Add(new DeviceSyncAddressEntry
            {
                AddressLine1 = a.AddressLine1,
                City = a.City,
                StateProvince = a.StateProvince,
                PostalCode = a.PostalCode,
                Country = a.Country,
                Tag = a.Tag
            });
        }

        foreach (var s in device.SocialProfiles)
        {
            request.SocialMedia.Add(new DeviceSyncSocialEntry
            {
                Service = s.Service,
                Username = s.Username,
                ProfileUrl = s.ProfileUrl
            });
        }

        return request;
    }

    /// <summary>
    /// Downloads a Gravatar image directly (not through the API client) since Gravatar
    /// is an external service that doesn't need auth. Handles 404 responses (d=404 parameter
    /// means "no Gravatar exists for this email").
    /// </summary>
    private static async Task<byte[]?> DownloadGravatarAsync(string gravatarUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(gravatarUrl);

            if (!response.IsSuccessStatusCode)
                return null;

            // Check content type is an image
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return null;

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validates that bytes represent a valid image by checking magic bytes.
    /// Supports JPEG, PNG, GIF, and WebP.
    /// </summary>
    private static bool IsValidImage(byte[] data)
    {
        if (data.Length < 4) return false;

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return true;

        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return true;

        // GIF: 47 49 46 38
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
            return true;

        // WebP: 52 49 46 46 ... 57 45 42 50
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return true;

        return false;
    }
}
