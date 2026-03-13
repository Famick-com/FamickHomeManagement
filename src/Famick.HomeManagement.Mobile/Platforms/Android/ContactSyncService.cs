using Android.Content;
using Android.Database;
using Android.Provider;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using Famick.HomeManagement.Shared.Contacts;
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
                    var deviceFieldsHash = ContactSyncMappingStore.ComputeContactFieldsHash(contact);
                    var existingDeviceId = _mappingStore.GetDeviceContactId(contact.Id);
                    var existingHash = _mappingStore.GetLastSyncedHash(contact.Id);

                    if (existingDeviceId != null && hash == existingHash)
                        continue;

                    if (existingDeviceId != null)
                    {
                        if (UpdateDeviceContact(resolver, existingDeviceId, contact))
                        {
                            _mappingStore.SetMapping(contact.Id, existingDeviceId, hash, deviceFieldsHash);
                            updated++;
                        }
                        else
                        {
                            // Update failed (stale ID) — delete old and recreate
                            DeleteDeviceContact(resolver, existingDeviceId);
                            var newDeviceId = CreateDeviceContact(resolver, contact, groupId);
                            if (newDeviceId != null)
                            {
                                _mappingStore.SetMapping(contact.Id, newDeviceId, hash, deviceFieldsHash);
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

    public Task<bool> SyncSingleContactToDeviceAsync(ContactDetailDto contact)
    {
        try
        {
            var resolver = Application.Context.ContentResolver;
            if (resolver == null) return Task.FromResult(false);

            var groupId = GetOrCreateFamickGroup(resolver);
            if (groupId < 0) return Task.FromResult(false);

            var hash = ContactSyncMappingStore.ComputeContactHash(contact);
            var deviceFieldsHash = ContactSyncMappingStore.ComputeContactFieldsHash(contact);
            var existingDeviceId = _mappingStore.GetDeviceContactId(contact.Id);

            if (existingDeviceId != null)
            {
                if (UpdateDeviceContact(resolver, existingDeviceId, contact))
                {
                    _mappingStore.SetMapping(contact.Id, existingDeviceId, hash, deviceFieldsHash);
                }
                else
                {
                    DeleteDeviceContact(resolver, existingDeviceId);
                    var newDeviceId = CreateDeviceContact(resolver, contact, groupId);
                    if (newDeviceId != null)
                        _mappingStore.SetMapping(contact.Id, newDeviceId, hash, deviceFieldsHash);
                    else
                        return Task.FromResult(false);
                }
            }
            else
            {
                var deviceId = CreateDeviceContact(resolver, contact, groupId);
                if (deviceId != null)
                    _mappingStore.SetMapping(contact.Id, deviceId, hash, deviceFieldsHash);
                else
                    return Task.FromResult(false);
            }

            _mappingStore.Save();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ContactSync] Android single contact sync failed: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> DeleteSingleContactFromDeviceAsync(Guid serverContactId)
    {
        try
        {
            var deviceId = _mappingStore.GetDeviceContactId(serverContactId);
            if (deviceId == null)
                return Task.FromResult(false);

            var resolver = Application.Context.ContentResolver;
            if (resolver == null)
                return Task.FromResult(false);

            var deleted = DeleteDeviceContact(resolver, deviceId);
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

    public Task<DeviceContactData?> ReadDeviceContactAsync(string deviceContactId)
    {
        try
        {
            if (!long.TryParse(deviceContactId, out var rawContactId))
                return Task.FromResult<DeviceContactData?>(null);

            var resolver = Application.Context.ContentResolver;
            if (resolver == null)
                return Task.FromResult<DeviceContactData?>(null);

            // Verify the raw contact still exists
            var rawUri = ContentUris.WithAppendedId(ContactsContract.RawContacts.ContentUri!, rawContactId);
            using var rawCursor = resolver.Query(rawUri, new[] { ContactsContract.RawContacts.InterfaceConsts.Id }, null, null, null);
            if (rawCursor == null || !rawCursor.MoveToFirst())
                return Task.FromResult<DeviceContactData?>(null);

            var data = new DeviceContactData();

            // Query all data rows for this raw contact
            using var cursor = resolver.Query(
                ContactsContract.Data.ContentUri!,
                new[]
                {
                    ContactsContract.Data.InterfaceConsts.Mimetype,   // 0
                    ContactsContract.Data.InterfaceConsts.Data1,      // 1
                    ContactsContract.Data.InterfaceConsts.Data2,      // 2
                    ContactsContract.Data.InterfaceConsts.Data3,      // 3
                    ContactsContract.Data.InterfaceConsts.Data4,      // 4
                    ContactsContract.Data.InterfaceConsts.Data5,      // 5
                    ContactsContract.Data.InterfaceConsts.Data6,      // 6
                    ContactsContract.Data.InterfaceConsts.Data7,      // 7
                    ContactsContract.Data.InterfaceConsts.Data8,      // 8
                    ContactsContract.Data.InterfaceConsts.Data9,      // 9
                    ContactsContract.Data.InterfaceConsts.Data10      // 10 (StructuredPostal.Country)
                },
                $"{ContactsContract.Data.InterfaceConsts.RawContactId} = ?",
                new[] { rawContactId.ToString() },
                null);

            if (cursor == null)
                return Task.FromResult<DeviceContactData?>(data);

            var hasStructuredName = false;

            while (cursor.MoveToNext())
            {
                var mimetype = cursor.GetString(0);
                switch (mimetype)
                {
                    case ContactsContract.CommonDataKinds.StructuredName.ContentItemType:
                        hasStructuredName = true;
                        data.FirstName = NullIfEmpty(cursor.GetString(2)); // DATA2 = GivenName
                        data.LastName = NullIfEmpty(cursor.GetString(3)); // DATA3 = FamilyName
                        data.MiddleName = NullIfEmpty(cursor.GetString(5)); // DATA5 = MiddleName
                        break;

                    case ContactsContract.CommonDataKinds.Organization.ContentItemType:
                        data.OrganizationName = NullIfEmpty(cursor.GetString(1)); // DATA1 = Company
                        data.JobTitle = NullIfEmpty(cursor.GetString(4)); // DATA4 = Title
                        break;

                    case ContactsContract.CommonDataKinds.Nickname.ContentItemType:
                        data.Nickname = NullIfEmpty(cursor.GetString(1)); // DATA1 = Name
                        break;

                    case ContactsContract.CommonDataKinds.Phone.ContentItemType:
                    {
                        var number = cursor.GetString(1); // DATA1 = Number
                        var type = cursor.GetInt(2); // DATA2 = Type
                        if (!string.IsNullOrEmpty(number))
                        {
                            data.PhoneNumbers.Add(new DevicePhoneEntry
                            {
                                PhoneNumber = number,
                                Tag = ReverseMapPhoneType(type)
                            });
                        }
                        break;
                    }

                    case ContactsContract.CommonDataKinds.Email.ContentItemType:
                    {
                        var email = cursor.GetString(1); // DATA1 = Address
                        var type = cursor.GetInt(2); // DATA2 = Type
                        if (!string.IsNullOrEmpty(email))
                        {
                            data.EmailAddresses.Add(new DeviceEmailEntry
                            {
                                Email = email,
                                Tag = ReverseMapEmailType(type)
                            });
                        }
                        break;
                    }

                    case ContactsContract.CommonDataKinds.StructuredPostal.ContentItemType:
                    {
                        var type = cursor.GetInt(2); // DATA2 = Type
                        data.Addresses.Add(new DeviceAddressEntry
                        {
                            AddressLine1 = NullIfEmpty(cursor.GetString(4)), // DATA4 = Street
                            City = NullIfEmpty(cursor.GetString(7)), // DATA7 = City
                            StateProvince = NullIfEmpty(cursor.GetString(8)), // DATA8 = Region
                            PostalCode = NullIfEmpty(cursor.GetString(9)), // DATA9 = Postcode
                            Country = NullIfEmpty(cursor.GetString(10)), // DATA10 = Country
                            Tag = ReverseMapAddressType(type)
                        });
                        break;
                    }

                    case ContactsContract.CommonDataKinds.Event.ContentItemType:
                    {
                        var type = cursor.GetInt(2); // DATA2 = Type
                        if (type == (int)EventDataKind.Birthday)
                        {
                            var dateStr = cursor.GetString(1); // DATA1 = StartDate
                            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var bday))
                            {
                                data.BirthYear = bday.Year;
                                data.BirthMonth = bday.Month;
                                data.BirthDay = bday.Day;
                            }
                        }
                        break;
                    }

                    case ContactsContract.CommonDataKinds.Note.ContentItemType:
                        data.Notes = NullIfEmpty(cursor.GetString(1)); // DATA1
                        break;

                    case ContactsContract.CommonDataKinds.Website.ContentItemType:
                        data.Website ??= NullIfEmpty(cursor.GetString(1)); // DATA1 = URL (take first)
                        break;
                }
            }

            // Determine if this is a group contact
            if (!hasStructuredName && !string.IsNullOrEmpty(data.OrganizationName))
            {
                data.IsGroup = true;
                data.DisplayName = data.OrganizationName;
            }

            return Task.FromResult<DeviceContactData?>(data);
        }
        catch
        {
            return Task.FromResult<DeviceContactData?>(null);
        }
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
                var rawContactId = ContentUris.ParseId(results[0].Uri!);

                // Write photo separately using ContentValues (byte[] doesn't work
                // reliably with ContentProviderOperation.WithValue)
                if (contact.PhotoData is { Length: > 0 })
                {
                    WriteContactPhoto(resolver, rawContactId, contact.PhotoData);
                }

                return rawContactId.ToString();
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

    private static void WriteContactPhoto(ContentResolver resolver, long rawContactId, byte[] photoData)
    {
        try
        {
            var values = new ContentValues();
            values.Put(ContactsContract.Data.InterfaceConsts.RawContactId, rawContactId);
            values.Put(ContactsContract.Data.InterfaceConsts.Mimetype,
                ContactsContract.CommonDataKinds.Photo.ContentItemType);
            values.Put(ContactsContract.CommonDataKinds.Photo.InterfaceConsts.Data15, photoData);

            resolver.Insert(ContactsContract.Data.ContentUri!, values);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ContactSync] Failed to write photo for rawContactId {rawContactId}: {ex.Message}");
        }
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

    private static int ReverseMapPhoneType(int type) => type switch
    {
        (int)PhoneDataKind.Mobile => 0,
        (int)PhoneDataKind.Home => 1,
        (int)PhoneDataKind.Work => 2,
        (int)PhoneDataKind.FaxWork or (int)PhoneDataKind.FaxHome => 3,
        _ => 99
    };

    private static int ReverseMapEmailType(int type) => type switch
    {
        (int)EmailDataKind.Home => 0,
        (int)EmailDataKind.Work => 1,
        _ => 99
    };

    private static int ReverseMapAddressType(int type) => type switch
    {
        (int)AddressDataKind.Home => 0,
        (int)AddressDataKind.Work => 1,
        _ => 99
    };

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;

    private static string? GetStringByColumnName(ICursor cursor, string? columnName)
    {
        if (columnName == null) return null;
        var index = cursor.GetColumnIndex(columnName);
        return index >= 0 ? cursor.GetString(index) : null;
    }

    #endregion
}
