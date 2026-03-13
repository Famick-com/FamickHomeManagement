using Contacts;
using Foundation;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using Famick.HomeManagement.Shared.Contacts;

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
                    var deviceFieldsHash = ContactSyncMappingStore.ComputeContactFieldsHash(contact);
                    var existingDeviceId = _mappingStore.GetDeviceContactId(contact.Id);
                    var existingHash = _mappingStore.GetLastSyncedHash(contact.Id);

                    if (existingDeviceId != null && hash == existingHash)
                        continue; // No changes

                    if (existingDeviceId != null)
                    {
                        // Delete and recreate to avoid stale unified contact ID issues
                        var newDeviceId = UpdateDeviceContact(store, existingDeviceId, contact, group);
                        if (newDeviceId != null)
                        {
                            _mappingStore.SetMapping(contact.Id, newDeviceId, hash, deviceFieldsHash);
                            updated++;
                        }
                        else
                            failed++;
                    }
                    else
                    {
                        // Create new
                        var deviceId = CreateDeviceContact(store, contact, group);
                        if (deviceId != null)
                        {
                            _mappingStore.SetMapping(contact.Id, deviceId, hash, deviceFieldsHash);
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

    public async Task<bool> SyncSingleContactToDeviceAsync(ContactDetailDto contact)
    {
        try
        {
            var store = new CNContactStore();
            var group = await GetOrCreateFamickGroupAsync(store);
            if (group == null) return false;

            var hash = ContactSyncMappingStore.ComputeContactHash(contact);
            var deviceFieldsHash = ContactSyncMappingStore.ComputeContactFieldsHash(contact);
            var existingDeviceId = _mappingStore.GetDeviceContactId(contact.Id);

            if (existingDeviceId != null)
            {
                var newDeviceId = UpdateDeviceContact(store, existingDeviceId, contact, group);
                if (newDeviceId != null)
                    _mappingStore.SetMapping(contact.Id, newDeviceId, hash, deviceFieldsHash);
                else
                    return false;
            }
            else
            {
                var deviceId = CreateDeviceContact(store, contact, group);
                if (deviceId != null)
                    _mappingStore.SetMapping(contact.Id, deviceId, hash, deviceFieldsHash);
                else
                    return false;
            }

            _mappingStore.Save();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> DeleteSingleContactFromDeviceAsync(Guid serverContactId)
    {
        try
        {
            var deviceId = _mappingStore.GetDeviceContactId(serverContactId);
            if (deviceId == null)
                return Task.FromResult(false);

            var store = new CNContactStore();
            var deleted = DeleteDeviceContact(store, deviceId);
            if (deleted)
            {
                _mappingStore.RemoveMapping(serverContactId);
                _mappingStore.Save();
            }
            return Task.FromResult(deleted);
        }
        catch
        {
            return Task.FromResult(false);
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

    public Task<DeviceContactData?> ReadDeviceContactAsync(string deviceContactId)
    {
        try
        {
            var store = new CNContactStore();
            var keysToFetch = new ICNKeyDescriptor[]
            {
                (ICNKeyDescriptor)CNContactKey.GivenName,
                (ICNKeyDescriptor)CNContactKey.FamilyName,
                (ICNKeyDescriptor)CNContactKey.MiddleName,
                (ICNKeyDescriptor)CNContactKey.OrganizationName,
                (ICNKeyDescriptor)CNContactKey.JobTitle,
                (ICNKeyDescriptor)CNContactKey.Nickname,
                (ICNKeyDescriptor)CNContactKey.PhoneNumbers,
                (ICNKeyDescriptor)CNContactKey.EmailAddresses,
                (ICNKeyDescriptor)CNContactKey.PostalAddresses,
                (ICNKeyDescriptor)CNContactKey.SocialProfiles,
                (ICNKeyDescriptor)CNContactKey.Birthday,
                (ICNKeyDescriptor)CNContactKey.UrlAddresses
            };

            var cnContact = store.GetUnifiedContact(deviceContactId, keysToFetch, out var error);
            if (cnContact == null || error != null)
                return Task.FromResult<DeviceContactData?>(null);

            var data = new DeviceContactData();

            // Determine if this is a group contact (org-only, no given/family name)
            var hasName = !string.IsNullOrEmpty(cnContact.GivenName) || !string.IsNullOrEmpty(cnContact.FamilyName);
            var hasOrg = !string.IsNullOrEmpty(cnContact.OrganizationName);

            if (!hasName && hasOrg)
            {
                data.IsGroup = true;
                data.DisplayName = cnContact.OrganizationName;
            }

            data.FirstName = NullIfEmpty(cnContact.GivenName);
            data.MiddleName = NullIfEmpty(cnContact.MiddleName);
            data.LastName = NullIfEmpty(cnContact.FamilyName);
            data.Nickname = NullIfEmpty(cnContact.Nickname);
            data.OrganizationName = NullIfEmpty(cnContact.OrganizationName);
            data.JobTitle = NullIfEmpty(cnContact.JobTitle);
            // Notes: Cannot read on iOS without com.apple.developer.contacts.notes entitlement
            data.Notes = null;

            // URLs → Website (take first)
            if (cnContact.UrlAddresses is { Length: > 0 })
            {
                var firstUrl = cnContact.UrlAddresses[0].Value?.ToString();
                data.Website = NullIfEmpty(firstUrl);
            }

            // Birthday
            if (cnContact.Birthday != null)
            {
                var bday = cnContact.Birthday;
                if (bday.Year != long.MinValue && bday.Year > 0)
                    data.BirthYear = (int)bday.Year;
                if (bday.Month != long.MinValue && bday.Month > 0)
                    data.BirthMonth = (int)bday.Month;
                if (bday.Day != long.MinValue && bday.Day > 0)
                    data.BirthDay = (int)bday.Day;
            }

            // Phone numbers — reverse-map iOS labels to tag ints
            if (cnContact.PhoneNumbers != null)
            {
                foreach (var labeled in cnContact.PhoneNumbers)
                {
                    var phone = labeled.Value;
                    if (phone == null) continue;
                    var tag = ReverseMapPhoneLabel(labeled.Label);
                    data.PhoneNumbers.Add(new DevicePhoneEntry
                    {
                        PhoneNumber = phone.StringValue ?? "",
                        Tag = tag
                    });
                }
            }

            // Email addresses
            if (cnContact.EmailAddresses != null)
            {
                foreach (var labeled in cnContact.EmailAddresses)
                {
                    var email = labeled.Value?.ToString();
                    if (string.IsNullOrEmpty(email)) continue;
                    var tag = ReverseMapEmailLabel(labeled.Label);
                    data.EmailAddresses.Add(new DeviceEmailEntry
                    {
                        Email = email,
                        Tag = tag
                    });
                }
            }

            // Postal addresses
            if (cnContact.PostalAddresses != null)
            {
                foreach (var labeled in cnContact.PostalAddresses)
                {
                    var addr = labeled.Value;
                    if (addr == null) continue;
                    var tag = ReverseMapAddressLabel(labeled.Label);
                    data.Addresses.Add(new DeviceAddressEntry
                    {
                        AddressLine1 = NullIfEmpty(addr.Street),
                        City = NullIfEmpty(addr.City),
                        StateProvince = NullIfEmpty(addr.State),
                        PostalCode = NullIfEmpty(addr.PostalCode),
                        Country = NullIfEmpty(addr.Country),
                        Tag = tag
                    });
                }
            }

            // Social profiles
            if (cnContact.SocialProfiles != null)
            {
                foreach (var labeled in cnContact.SocialProfiles)
                {
                    var profile = labeled.Value;
                    if (profile == null) continue;
                    var service = ReverseMapSocialService(profile.Service);
                    data.SocialProfiles.Add(new DeviceSocialEntry
                    {
                        Service = service,
                        Username = profile.Username ?? "",
                        ProfileUrl = NullIfEmpty(profile.UrlString)
                    });
                }
            }

            return Task.FromResult<DeviceContactData?>(data);
        }
        catch
        {
            return Task.FromResult<DeviceContactData?>(null);
        }
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

    private static string? UpdateDeviceContact(CNContactStore store, string deviceId, ContactDetailDto contact, CNGroup group)
    {
        // Delete and recreate (same as Android). In-place updates are fragile with
        // iOS unified contacts — the stored identifier can become stale when iOS
        // merges/unmerges contacts, causing CNErrorDomain Code=200.
        // Wrap delete in try/catch — if the old contact no longer exists,
        // ExecuteSaveRequest throws rather than returning false.
        try { DeleteDeviceContact(store, deviceId); }
        catch { /* Old contact already gone — fine, just create new */ }

        return CreateDeviceContact(store, contact, group);
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
        // Note: CNContactKey.Note requires com.apple.developer.contacts.notes entitlement (iOS 13+).
        // Without it, setting Note causes save to fail with CNErrorDomain Code=102.
        // Skip writing notes until the entitlement is provisioned.

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

        // Profile photo
        if (contact.PhotoData is { Length: > 0 })
            cnContact.ImageData = NSData.FromArray(contact.PhotoData);
        else
            cnContact.ImageData = null;
    }

    private static int ReverseMapPhoneLabel(string? label)
    {
        if (label == null) return 99;
        // iOS uses decorated labels like "_$!<Mobile>!$_" and CNLabelKey constants
        if (label.Contains("Mobile", StringComparison.OrdinalIgnoreCase)) return 0;
        if (label == CNLabelKey.Home || label.Contains("Home", StringComparison.OrdinalIgnoreCase)) return 1;
        if (label == CNLabelKey.Work || label.Contains("Work", StringComparison.OrdinalIgnoreCase))
        {
            if (label.Contains("FAX", StringComparison.OrdinalIgnoreCase)) return 3;
            return 2;
        }
        return 99;
    }

    private static int ReverseMapEmailLabel(string? label)
    {
        if (label == null) return 99;
        if (label == CNLabelKey.Home || label.Contains("Home", StringComparison.OrdinalIgnoreCase)) return 0;
        if (label == CNLabelKey.Work || label.Contains("Work", StringComparison.OrdinalIgnoreCase)) return 1;
        return 99;
    }

    private static int ReverseMapAddressLabel(string? label)
    {
        if (label == null) return 99;
        if (label == CNLabelKey.Home || label.Contains("Home", StringComparison.OrdinalIgnoreCase)) return 0;
        if (label == CNLabelKey.Work || label.Contains("Work", StringComparison.OrdinalIgnoreCase)) return 1;
        return 99;
    }

    private static int ReverseMapSocialService(string? service)
    {
        if (service == null) return 0;
        if (service == CNSocialProfileServiceKey.Facebook || service.Equals("Facebook", StringComparison.OrdinalIgnoreCase)) return 1;
        if (service == CNSocialProfileServiceKey.Twitter || service.Equals("Twitter", StringComparison.OrdinalIgnoreCase) || service.Equals("X", StringComparison.OrdinalIgnoreCase)) return 2;
        if (service.Equals("Instagram", StringComparison.OrdinalIgnoreCase)) return 3;
        if (service == CNSocialProfileServiceKey.LinkedIn || service.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase)) return 4;
        return 0;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;

    #endregion
}
