using Android.Content;
using Android.Database;
using Android.Provider;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using Application = Android.App.Application;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android implementation of contact sync using ContactsContract.
/// Syncs contacts into a "Famick" group in the device's Contacts app.
/// </summary>
public class ContactSyncService : IContactSyncService
{
    private const string FamickGroupName = "Famick";
    private const string FamickAccountName = "Famick";
    private const string FamickAccountType = "com.famick.homemanagement";
    private readonly ContactSyncMappingStore _mappingStore;

    public ContactSyncService(ContactSyncMappingStore mappingStore)
    {
        _mappingStore = mappingStore;
    }

    public async Task<bool> RequestPermissionAsync()
    {
        var readStatus = await Permissions.RequestAsync<Permissions.ContactsRead>();
        var writeStatus = await Permissions.RequestAsync<Permissions.ContactsWrite>();
        return readStatus == PermissionStatus.Granted && writeStatus == PermissionStatus.Granted;
    }

    public async Task<bool> HasPermissionAsync()
    {
        var readStatus = await Permissions.CheckStatusAsync<Permissions.ContactsRead>();
        var writeStatus = await Permissions.CheckStatusAsync<Permissions.ContactsWrite>();
        return readStatus == PermissionStatus.Granted && writeStatus == PermissionStatus.Granted;
    }

    public async Task<ContactSyncResult> SyncContactsAsync(List<ContactDetailDto> contacts, CancellationToken ct = default)
    {
        try
        {
            var resolver = Application.Context.ContentResolver;
            if (resolver == null)
                return ContactSyncResult.Fail("ContentResolver not available");

            var groupId = GetOrCreateFamickGroup(resolver);
            if (groupId < 0)
                return ContactSyncResult.Fail("Failed to create Famick group");

            var serverContactIds = contacts.Select(c => c.Id).ToHashSet();
            var created = 0;
            var updated = 0;
            var deleted = 0;
            var failed = 0;

            foreach (var contact in contacts)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var hash = ContactSyncMappingStore.ComputeContactHash(contact);
                    var existingDeviceId = _mappingStore.GetDeviceContactId(contact.Id);
                    var existingHash = _mappingStore.GetLastSyncedHash(contact.Id);

                    if (existingDeviceId != null && hash == existingHash)
                        continue;

                    if (existingDeviceId != null)
                    {
                        if (UpdateDeviceContact(resolver, existingDeviceId, contact))
                        {
                            _mappingStore.SetMapping(contact.Id, existingDeviceId, hash);
                            updated++;
                        }
                        else
                        {
                            // Update failed (stale ID) — delete old and recreate
                            DeleteDeviceContact(resolver, existingDeviceId);
                            var newDeviceId = CreateDeviceContact(resolver, contact, groupId);
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
                        var deviceId = CreateDeviceContact(resolver, contact, groupId);
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

            // Delete contacts no longer on server
            var syncedIds = _mappingStore.GetAllSyncedServerContactIds();
            foreach (var syncedId in syncedIds)
            {
                if (!serverContactIds.Contains(syncedId))
                {
                    var deviceId = _mappingStore.GetDeviceContactId(syncedId);
                    if (deviceId != null && DeleteDeviceContact(resolver, deviceId))
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
            var resolver = Application.Context.ContentResolver;
            if (resolver == null)
                return Task.FromResult(ContactSyncResult.Fail("ContentResolver not available"));

            var syncedIds = _mappingStore.GetAllSyncedServerContactIds();
            var deleted = 0;

            foreach (var syncedId in syncedIds)
            {
                ct.ThrowIfCancellationRequested();
                var deviceId = _mappingStore.GetDeviceContactId(syncedId);
                if (deviceId != null && DeleteDeviceContact(resolver, deviceId))
                    deleted++;
            }

            RemoveFamickGroup(resolver);
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

    private static long GetOrCreateFamickGroup(ContentResolver resolver)
    {
        // Find existing group
        var groupUri = ContactsContract.Groups.ContentUri;
        if (groupUri == null) return -1;

        using var cursor = resolver.Query(
            groupUri,
            new[] { ContactsContract.Groups.InterfaceConsts.Id, ContactsContract.Groups.InterfaceConsts.Title },
            $"{ContactsContract.Groups.InterfaceConsts.Title} = ?",
            new[] { FamickGroupName },
            null);

        if (cursor != null && cursor.MoveToFirst())
        {
            return cursor.GetLong(0);
        }

        // Create new group
        var values = new ContentValues();
        values.Put(ContactsContract.Groups.InterfaceConsts.Title, FamickGroupName);
        values.Put(ContactsContract.Groups.InterfaceConsts.AccountName, FamickAccountName);
        values.Put(ContactsContract.Groups.InterfaceConsts.AccountType, FamickAccountType);

        var result = resolver.Insert(groupUri, values);
        return result != null ? ContentUris.ParseId(result) : -1;
    }

    private static string? CreateDeviceContact(ContentResolver resolver, ContactDetailDto contact, long groupId)
    {
        var ops = new List<ContentProviderOperation>();
        var rawContactIndex = 0;

        // Insert raw contact
        var rawContactOp = ContentProviderOperation.NewInsert(ContactsContract.RawContacts.ContentUri!)
            .WithValue(ContactsContract.RawContacts.InterfaceConsts.AccountName, FamickAccountName)
            .WithValue(ContactsContract.RawContacts.InterfaceConsts.AccountType, FamickAccountType)
            .Build();
        ops.Add(rawContactOp);

        if (contact.IsGroup)
        {
            // Group contacts (households/businesses) use DisplayName as the organization
            var orgOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.Organization.ContentItemType)
                .WithValue(ContactsContract.CommonDataKinds.Organization.Company, contact.DisplayName ?? contact.CompanyName ?? "")
                .Build();
            ops.Add(orgOp);
        }
        else
        {
            // Name
            var nameOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.StructuredName.ContentItemType)
                .WithValue(ContactsContract.CommonDataKinds.StructuredName.GivenName, contact.FirstName ?? "")
                .WithValue(ContactsContract.CommonDataKinds.StructuredName.FamilyName, contact.LastName ?? "")
                .WithValue(ContactsContract.CommonDataKinds.StructuredName.MiddleName, contact.MiddleName ?? "")
                .Build();
            ops.Add(nameOp);

            // Organization
            if (!string.IsNullOrWhiteSpace(contact.CompanyName) || !string.IsNullOrWhiteSpace(contact.Title))
            {
                var orgOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                    .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                    .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.Organization.ContentItemType)
                    .WithValue(ContactsContract.CommonDataKinds.Organization.Company, contact.CompanyName ?? "")
                    .WithValue(ContactsContract.CommonDataKinds.Organization.Title, contact.Title ?? "")
                    .Build();
                ops.Add(orgOp);
            }
        }

        // Nickname
        if (!string.IsNullOrWhiteSpace(contact.PreferredName) && contact.PreferredName != contact.FirstName)
        {
            var nickOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.Nickname.ContentItemType)
                .WithValue(ContactsContract.CommonDataKinds.Nickname.Name, contact.PreferredName)
                .Build();
            ops.Add(nickOp);
        }

        // Phone numbers
        foreach (var phone in contact.PhoneNumbers)
        {
            var phoneType = phone.Tag switch
            {
                0 => (int)PhoneDataKind.Mobile,
                1 => (int)PhoneDataKind.Home,
                2 => (int)PhoneDataKind.Work,
                3 => (int)PhoneDataKind.FaxWork,
                _ => (int)PhoneDataKind.Other
            };

            var phoneOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.Phone.ContentItemType)
                .WithValue(ContactsContract.CommonDataKinds.Phone.Number, phone.PhoneNumber)
                .WithValue(ContactsContract.CommonDataKinds.Phone.InterfaceConsts.Type, phoneType)
                .Build();
            ops.Add(phoneOp);
        }

        // Email addresses
        foreach (var email in contact.EmailAddresses)
        {
            var emailType = email.Tag switch
            {
                0 => (int)EmailDataKind.Home,
                1 => (int)EmailDataKind.Work,
                _ => (int)EmailDataKind.Other
            };

            var emailOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.Email.ContentItemType)
                .WithValue(ContactsContract.CommonDataKinds.Email.InterfaceConsts.Data, email.Email)
                .WithValue(ContactsContract.CommonDataKinds.Email.InterfaceConsts.Type, emailType)
                .Build();
            ops.Add(emailOp);
        }

        // Addresses
        foreach (var addr in contact.Addresses.Where(a => a.Address != null))
        {
            var addrType = addr.Tag switch
            {
                0 => (int)AddressDataKind.Home,
                1 => (int)AddressDataKind.Work,
                _ => (int)AddressDataKind.Other
            };

            var addrOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.StructuredPostal.ContentItemType)
                .WithValue(ContactsContract.CommonDataKinds.StructuredPostal.Street, addr.Address!.AddressLine1 ?? "")
                .WithValue(ContactsContract.CommonDataKinds.StructuredPostal.City, addr.Address.City ?? "")
                .WithValue(ContactsContract.CommonDataKinds.StructuredPostal.Region, addr.Address.StateProvince ?? "")
                .WithValue(ContactsContract.CommonDataKinds.StructuredPostal.Postcode, addr.Address.PostalCode ?? "")
                .WithValue(ContactsContract.CommonDataKinds.StructuredPostal.Country, addr.Address.Country ?? "")
                .WithValue(ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Type, addrType)
                .Build();
            ops.Add(addrOp);
        }

        // Website
        if (!string.IsNullOrWhiteSpace(contact.Website))
        {
            var webOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.Website.ContentItemType)
                .WithValue(ContactsContract.CommonDataKinds.Website.Url, contact.Website)
                .Build();
            ops.Add(webOp);
        }

        // Note
        if (!string.IsNullOrWhiteSpace(contact.Notes))
        {
            var noteOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.Note.ContentItemType)
                .WithValue(ContactsContract.CommonDataKinds.Note.InterfaceConsts.Data1, contact.Notes)
                .Build();
            ops.Add(noteOp);
        }

        // Birthday
        if (contact.BirthDatePrecision >= 3 && contact.BirthYear.HasValue &&
            contact.BirthMonth.HasValue && contact.BirthDay.HasValue)
        {
            var bday = $"{contact.BirthYear:D4}-{contact.BirthMonth:D2}-{contact.BirthDay:D2}";
            var bdayOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
                .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
                .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.Event.ContentItemType)
                .WithValue(ContactsContract.CommonDataKinds.Event.StartDate, bday)
                .WithValue(ContactsContract.CommonDataKinds.Event.InterfaceConsts.Type, (int)EventDataKind.Birthday)
                .Build();
            ops.Add(bdayOp);
        }

        // Group membership
        var groupOp = ContentProviderOperation.NewInsert(ContactsContract.Data.ContentUri!)
            .WithValueBackReference(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactIndex)
            .WithValue(ContactsContract.Data.InterfaceConsts.Mimetype, ContactsContract.CommonDataKinds.GroupMembership.ContentItemType)
            .WithValue(ContactsContract.CommonDataKinds.GroupMembership.GroupRowId, groupId)
            .Build();
        ops.Add(groupOp);

        try
        {
            var results = resolver.ApplyBatch(ContactsContract.Authority, ops);
            if (results != null && results.Length > 0 && results[0]?.Uri != null)
            {
                return ContentUris.ParseId(results[0].Uri!).ToString();
            }
        }
        catch
        {
            // Fall through
        }

        return null;
    }

    private static bool UpdateDeviceContact(ContentResolver resolver, string deviceId, ContactDetailDto contact)
    {
        // Simple approach: delete and recreate
        // This is safe because we control the Famick account contacts
        if (DeleteDeviceContact(resolver, deviceId))
        {
            var groupId = GetOrCreateFamickGroup(resolver);
            if (groupId >= 0)
            {
                var newId = CreateDeviceContact(resolver, contact, groupId);
                return newId != null;
            }
        }
        return false;
    }

    private static bool DeleteDeviceContact(ContentResolver resolver, string deviceId)
    {
        if (!long.TryParse(deviceId, out var rawContactId))
            return false;

        var uri = ContentUris.WithAppendedId(ContactsContract.RawContacts.ContentUri!, rawContactId);
        var deleted = resolver.Delete(uri, null, null);
        return deleted > 0;
    }

    private static void RemoveFamickGroup(ContentResolver resolver)
    {
        var groupUri = ContactsContract.Groups.ContentUri;
        if (groupUri == null) return;

        resolver.Delete(
            groupUri,
            $"{ContactsContract.Groups.InterfaceConsts.Title} = ?",
            new[] { FamickGroupName });
    }

    #endregion
}
