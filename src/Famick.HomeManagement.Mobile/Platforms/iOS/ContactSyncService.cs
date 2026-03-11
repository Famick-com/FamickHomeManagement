using Contacts;
using Foundation;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// iOS implementation of contact sync using the Contacts framework (CNContactStore).
/// Syncs contacts into a "Famick" group in the device's Contacts app.
/// </summary>
public class ContactSyncService : IContactSyncService
{
    private const string FamickGroupName = "Famick";
    private readonly ContactSyncMappingStore _mappingStore;

    public ContactSyncService(ContactSyncMappingStore mappingStore)
    {
        _mappingStore = mappingStore;
    }

    public Task<bool> RequestPermissionAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        var store = new CNContactStore();
        store.RequestAccess(CNEntityType.Contacts, (granted, error) =>
        {
            tcs.SetResult(granted);
        });
        return tcs.Task;
    }

    public Task<bool> HasPermissionAsync()
    {
        var status = CNContactStore.GetAuthorizationStatus(CNEntityType.Contacts);
        return Task.FromResult(status == CNAuthorizationStatus.Authorized);
    }

    public async Task<ContactSyncResult> SyncContactsAsync(List<ContactDetailDto> contacts, CancellationToken ct = default)
    {
        try
        {
            var store = new CNContactStore();
            var group = await GetOrCreateFamickGroupAsync(store);
            if (group == null)
                return ContactSyncResult.Fail("Failed to create Famick group");

            var serverContactIds = contacts.Select(c => c.Id).ToHashSet();
            var created = 0;
            var updated = 0;
            var deleted = 0;
            var failed = 0;

            // Create or update contacts
            foreach (var contact in contacts)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var hash = ContactSyncMappingStore.ComputeContactHash(contact);
                    var existingDeviceId = _mappingStore.GetDeviceContactId(contact.Id);
                    var existingHash = _mappingStore.GetLastSyncedHash(contact.Id);

                    if (existingDeviceId != null && hash == existingHash)
                        continue; // No changes

                    if (existingDeviceId != null)
                    {
                        // Update existing; if update fails, delete and recreate
                        if (UpdateDeviceContact(store, existingDeviceId, contact, group))
                        {
                            _mappingStore.SetMapping(contact.Id, existingDeviceId, hash);
                            updated++;
                        }
                        else
                        {
                            // Update failed (stale ID) — delete old and recreate
                            DeleteDeviceContact(store, existingDeviceId);
                            var newDeviceId = CreateDeviceContact(store, contact, group);
                            if (newDeviceId != null)
                            {
                                _mappingStore.SetMapping(contact.Id, newDeviceId, hash);
                                updated++;
                            }
                            else
                                failed++;
                        }
                    }
                    else
                    {
                        // Create new
                        var deviceId = CreateDeviceContact(store, contact, group);
                        if (deviceId != null)
                        {
                            _mappingStore.SetMapping(contact.Id, deviceId, hash);
                            created++;
                        }
                        else
                            failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            // Delete contacts that are no longer on the server
            var syncedIds = _mappingStore.GetAllSyncedServerContactIds();
            foreach (var syncedId in syncedIds)
            {
                if (!serverContactIds.Contains(syncedId))
                {
                    var deviceId = _mappingStore.GetDeviceContactId(syncedId);
                    if (deviceId != null && DeleteDeviceContact(store, deviceId))
                    {
                        _mappingStore.RemoveMapping(syncedId);
                        deleted++;
                    }
                }
            }

            _mappingStore.Save();
            var result = ContactSyncResult.Ok(created, updated, deleted);
            result.Failed = failed;
            return result;
        }
        catch (Exception ex)
        {
            return ContactSyncResult.Fail($"Sync failed: {ex.Message}");
        }
    }

    public Task<ContactSyncResult> RemoveAllSyncedContactsAsync(CancellationToken ct = default)
    {
        try
        {
            var store = new CNContactStore();
            var syncedIds = _mappingStore.GetAllSyncedServerContactIds();
            var deleted = 0;

            foreach (var syncedId in syncedIds)
            {
                ct.ThrowIfCancellationRequested();
                var deviceId = _mappingStore.GetDeviceContactId(syncedId);
                if (deviceId != null && DeleteDeviceContact(store, deviceId))
                    deleted++;
            }

            // Also try to remove the Famick group
            RemoveFamickGroup(store);

            _mappingStore.Clear();
            return Task.FromResult(ContactSyncResult.Ok(0, 0, deleted));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ContactSyncResult.Fail($"Remove failed: {ex.Message}"));
        }
    }

    public async Task<ContactSyncStatus> GetSyncStatusAsync()
    {
        var hasPermission = await HasPermissionAsync();
        return new ContactSyncStatus
        {
            SyncedCount = _mappingStore.SyncedCount,
            LastSyncedAt = _mappingStore.LastSyncedAt,
            HasPermission = hasPermission
        };
    }

    #region Private Methods

    private static Task<CNGroup?> GetOrCreateFamickGroupAsync(CNContactStore store)
    {
        // Find existing Famick group
        var groups = store.GetGroups(null, out var error);
        if (error == null && groups != null)
        {
            foreach (var g in groups)
            {
                if (g.Name == FamickGroupName)
                    return Task.FromResult<CNGroup?>(g);
            }
        }

        // Create new group
        var saveRequest = new CNSaveRequest();
        var newGroup = new CNMutableGroup { Name = FamickGroupName };
        saveRequest.AddGroup(newGroup, null);

        if (store.ExecuteSaveRequest(saveRequest, out error))
            return Task.FromResult<CNGroup?>((CNGroup)newGroup);

        return Task.FromResult<CNGroup?>(null);
    }

    private static string? CreateDeviceContact(CNContactStore store, ContactDetailDto contact, CNGroup group)
    {
        var cnContact = new CNMutableContact();
        MapContactFields(cnContact, contact);

        var saveRequest = new CNSaveRequest();
        saveRequest.AddContact(cnContact, null);
        saveRequest.AddMember(cnContact, group);

        if (store.ExecuteSaveRequest(saveRequest, out _))
            return cnContact.Identifier;

        return null;
    }

    private static bool UpdateDeviceContact(CNContactStore store, string deviceId, ContactDetailDto contact, CNGroup group)
    {
        var keysToFetch = new[]
        {
            CNContactKey.GivenName, CNContactKey.FamilyName, CNContactKey.MiddleName,
            CNContactKey.Nickname, CNContactKey.OrganizationName, CNContactKey.JobTitle,
            CNContactKey.PhoneNumbers, CNContactKey.EmailAddresses, CNContactKey.PostalAddresses,
            CNContactKey.UrlAddresses, CNContactKey.SocialProfiles, CNContactKey.Birthday,
            CNContactKey.Note
        };

        var existing = store.GetUnifiedContact(deviceId, keysToFetch, out var error);
        if (existing == null || error != null)
            return false;

        var mutable = existing.MutableCopy() as CNMutableContact;
        if (mutable == null)
            return false;

        MapContactFields(mutable, contact);

        var saveRequest = new CNSaveRequest();
        saveRequest.UpdateContact(mutable);

        return store.ExecuteSaveRequest(saveRequest, out _);
    }

    private static bool DeleteDeviceContact(CNContactStore store, string deviceId)
    {
        var keysToFetch = new[] { CNContactKey.Identifier };
        var existing = store.GetUnifiedContact(deviceId, keysToFetch, out var error);
        if (existing == null || error != null)
            return false;

        var mutable = existing.MutableCopy() as CNMutableContact;
        if (mutable == null)
            return false;

        var saveRequest = new CNSaveRequest();
        saveRequest.DeleteContact(mutable);

        return store.ExecuteSaveRequest(saveRequest, out _);
    }

    private static void RemoveFamickGroup(CNContactStore store)
    {
        var groups = store.GetGroups(null, out _);
        if (groups == null) return;

        foreach (var g in groups)
        {
            if (g.Name == FamickGroupName)
            {
                var mutable = g.MutableCopy() as CNMutableGroup;
                if (mutable == null) continue;

                var saveRequest = new CNSaveRequest();
                saveRequest.DeleteGroup(mutable);
                store.ExecuteSaveRequest(saveRequest, out _);
                break;
            }
        }
    }

    private static void MapContactFields(CNMutableContact cnContact, ContactDetailDto contact)
    {
        if (contact.IsGroup)
        {
            // Group contacts (households/businesses) use DisplayName as the organization
            cnContact.OrganizationName = contact.DisplayName ?? contact.CompanyName ?? "";
        }
        else
        {
            cnContact.GivenName = contact.FirstName ?? "";
            cnContact.FamilyName = contact.LastName ?? "";
            cnContact.MiddleName = contact.MiddleName ?? "";
            cnContact.OrganizationName = contact.CompanyName ?? "";
            cnContact.JobTitle = contact.Title ?? "";
        }
        cnContact.Note = contact.Notes ?? "";

        if (!string.IsNullOrWhiteSpace(contact.PreferredName) && contact.PreferredName != contact.FirstName)
            cnContact.Nickname = contact.PreferredName;

        // Phone numbers
        var phones = contact.PhoneNumbers.Select(p =>
        {
            // Use raw label strings from the Contacts framework
            var label = p.Tag switch
            {
                0 => "_$!<Mobile>!$_",
                1 => CNLabelKey.Home,
                2 => CNLabelKey.Work,
                3 => "_$!<WorkFAX>!$_",
                _ => CNLabelKey.Other
            };
            return new CNLabeledValue<CNPhoneNumber>(label, new CNPhoneNumber(p.PhoneNumber));
        }).ToArray();
        cnContact.PhoneNumbers = phones;

        // Email addresses
        var emails = contact.EmailAddresses.Select(e =>
        {
            var label = e.Tag switch
            {
                0 => CNLabelKey.Home,
                1 => CNLabelKey.Work,
                _ => CNLabelKey.Other
            };
            return new CNLabeledValue<NSString>(label, new NSString(e.Email));
        }).ToArray();
        cnContact.EmailAddresses = emails;

        // Postal addresses
        var addressList = new List<CNLabeledValue<CNPostalAddress>>();
        foreach (var a in contact.Addresses.Where(a => a.Address != null))
        {
            var label = a.Tag switch
            {
                0 => CNLabelKey.Home,
                1 => CNLabelKey.Work,
                _ => CNLabelKey.Other
            };
            var postal = new CNMutablePostalAddress();
            postal.Street = a.Address!.AddressLine1 ?? "";
            postal.City = a.Address.City ?? "";
            postal.State = a.Address.StateProvince ?? "";
            postal.PostalCode = a.Address.PostalCode ?? "";
            postal.Country = a.Address.Country ?? "";
            addressList.Add(new CNLabeledValue<CNPostalAddress>(label, postal));
        }
        cnContact.PostalAddresses = addressList.ToArray();

        // URLs
        var urls = new List<CNLabeledValue<NSString>>();
        if (!string.IsNullOrWhiteSpace(contact.Website))
            urls.Add(new CNLabeledValue<NSString>(CNLabelKey.Home, new NSString(contact.Website)));
        cnContact.UrlAddresses = urls.ToArray();

        // Social profiles
        var socials = contact.SocialMedia.Select(s =>
        {
            var service = s.Service switch
            {
                1 => CNSocialProfileServiceKey.Facebook,
                2 => CNSocialProfileServiceKey.Twitter,
                3 => "Instagram",
                4 => CNSocialProfileServiceKey.LinkedIn,
                _ => s.ServiceLabel
            };
            var profile = new CNSocialProfile(
                s.ProfileUrl ?? "",
                s.Username,
                "",
                service ?? "Other");
            return new CNLabeledValue<CNSocialProfile>(service ?? "Other", profile);
        }).ToArray();
        cnContact.SocialProfiles = socials;

        // Birthday
        if (contact.BirthDatePrecision >= 3 && contact.BirthYear.HasValue &&
            contact.BirthMonth.HasValue && contact.BirthDay.HasValue)
        {
            var components = new NSDateComponents
            {
                Year = contact.BirthYear.Value,
                Month = contact.BirthMonth.Value,
                Day = contact.BirthDay.Value
            };
            cnContact.Birthday = components;
        }
        else if (contact.BirthDatePrecision >= 2 && contact.BirthMonth.HasValue && contact.BirthDay.HasValue)
        {
            var components = new NSDateComponents
            {
                Month = contact.BirthMonth.Value,
                Day = contact.BirthDay.Value
            };
            cnContact.Birthday = components;
        }
    }

    #endregion
}
