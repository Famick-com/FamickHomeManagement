using Famick.HomeManagement.Mobile.Models;

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
    /// Runs a full sync: fetches all contacts from server and syncs them to the device.
    /// </summary>
    public async Task<ContactSyncResult> SyncAsync(CancellationToken ct = default)
    {
        // Check permission
        var hasPermission = await _syncService.HasPermissionAsync();
        if (!hasPermission)
        {
            return ContactSyncResult.Fail("Contact permission not granted");
        }

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
}
