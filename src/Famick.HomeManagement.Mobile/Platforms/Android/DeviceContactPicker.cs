using Android.App;
using Android.Content;
using Android.Provider;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

public class DeviceContactPicker : IDeviceContactPicker
{
    private static TaskCompletionSource<SharedContactData?>? _pendingTcs;
    private const int PickContactRequestCode = 9001;

    public Task<SharedContactData?> PickContactAsync()
    {
        _pendingTcs = new TaskCompletionSource<SharedContactData?>();

        var intent = new Intent(Intent.ActionPick, ContactsContract.Contacts.ContentUri);
        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            _pendingTcs.SetResult(null);
            return _pendingTcs.Task;
        }

        activity.StartActivityForResult(intent, PickContactRequestCode);
        return _pendingTcs.Task;
    }

    public static void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != PickContactRequestCode) return;

        if (resultCode != Result.Ok || data?.Data == null)
        {
            _pendingTcs?.TrySetResult(null);
            return;
        }

        try
        {
            var contactData = ReadContact(data.Data);
            _pendingTcs?.TrySetResult(contactData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeviceContactPicker] Error reading contact: {ex.Message}");
            _pendingTcs?.TrySetResult(null);
        }
    }

    private static SharedContactData? ReadContact(global::Android.Net.Uri contactUri)
    {
        var resolver = Platform.CurrentActivity?.ContentResolver;
        if (resolver == null) return null;

        // Get contact ID
        string? contactId = null;
        using (var cursor = resolver.Query(contactUri, new[] { ContactsContract.Contacts.InterfaceConsts.Id }, null, null, null))
        {
            if (cursor == null || !cursor.MoveToFirst()) return null;
            contactId = cursor.GetString(0);
        }

        if (contactId == null) return null;

        var data = new SharedContactData();

        // Read structured name
        ReadName(resolver, contactId, data);

        // Read organization
        ReadOrganization(resolver, contactId, data);

        // Read phone numbers
        ReadPhones(resolver, contactId, data);

        // Read emails
        ReadEmails(resolver, contactId, data);

        // Read addresses
        ReadAddresses(resolver, contactId, data);

        // Read notes
        ReadNotes(resolver, contactId, data);

        return data;
    }

    private static void ReadName(ContentResolver resolver, string contactId, SharedContactData data)
    {
        var uri = ContactsContract.Data.ContentUri;
        var selection = $"{ContactsContract.Data.InterfaceConsts.ContactId} = ? AND {ContactsContract.Data.InterfaceConsts.Mimetype} = ?";
        var selectionArgs = new[] { contactId, ContactsContract.CommonDataKinds.StructuredName.ContentItemType };

        using var cursor = resolver.Query(uri, null, selection, selectionArgs, null);
        if (cursor != null && cursor.MoveToFirst())
        {
            data.FirstName = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.StructuredName.GivenName)));
            data.MiddleName = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.StructuredName.MiddleName)));
            data.LastName = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.StructuredName.FamilyName)));
        }
    }

    private static void ReadOrganization(ContentResolver resolver, string contactId, SharedContactData data)
    {
        var uri = ContactsContract.Data.ContentUri;
        var selection = $"{ContactsContract.Data.InterfaceConsts.ContactId} = ? AND {ContactsContract.Data.InterfaceConsts.Mimetype} = ?";
        var selectionArgs = new[] { contactId, ContactsContract.CommonDataKinds.Organization.ContentItemType };

        using var cursor = resolver.Query(uri, null, selection, selectionArgs, null);
        if (cursor != null && cursor.MoveToFirst())
        {
            data.CompanyName = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.Organization.Company)));
            data.Title = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.Organization.Title)));
        }
    }

    private static void ReadPhones(ContentResolver resolver, string contactId, SharedContactData data)
    {
        var uri = ContactsContract.CommonDataKinds.Phone.ContentUri;
        var selection = $"{ContactsContract.CommonDataKinds.Phone.InterfaceConsts.ContactId} = ?";

        using var cursor = resolver.Query(uri, null, selection, new[] { contactId }, null);
        if (cursor == null) return;

        while (cursor.MoveToNext())
        {
            var number = cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.Phone.Number));
            if (string.IsNullOrWhiteSpace(number)) continue;

            var type = cursor.GetInt(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.Phone.InterfaceConsts.Type));
            data.PhoneNumbers.Add(new SharedPhoneEntry
            {
                PhoneNumber = number,
                Tag = MapPhoneType(type)
            });
        }
    }

    private static void ReadEmails(ContentResolver resolver, string contactId, SharedContactData data)
    {
        var uri = ContactsContract.CommonDataKinds.Email.ContentUri;
        var selection = $"{ContactsContract.CommonDataKinds.Email.InterfaceConsts.ContactId} = ?";

        using var cursor = resolver.Query(uri, null, selection, new[] { contactId }, null);
        if (cursor == null) return;

        while (cursor.MoveToNext())
        {
            var address = cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.Email.Address));
            if (string.IsNullOrWhiteSpace(address)) continue;

            var type = cursor.GetInt(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.Email.InterfaceConsts.Type));
            data.EmailAddresses.Add(new SharedEmailEntry
            {
                Email = address,
                Tag = MapEmailType(type)
            });
        }
    }

    private static void ReadAddresses(ContentResolver resolver, string contactId, SharedContactData data)
    {
        var uri = ContactsContract.Data.ContentUri;
        var selection = $"{ContactsContract.Data.InterfaceConsts.ContactId} = ? AND {ContactsContract.Data.InterfaceConsts.Mimetype} = ?";
        var selectionArgs = new[] { contactId, ContactsContract.CommonDataKinds.StructuredPostal.ContentItemType };

        using var cursor = resolver.Query(uri, null, selection, selectionArgs, null);
        if (cursor == null) return;

        while (cursor.MoveToNext())
        {
            var street = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.StructuredPostal.Street)));
            var city = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.StructuredPostal.City)));
            var postalCode = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.StructuredPostal.Postcode)));

            if (street == null && city == null && postalCode == null) continue;

            var type = cursor.GetInt(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.StructuredPostal.InterfaceConsts.Type));
            data.Addresses.Add(new SharedAddressEntry
            {
                AddressLine1 = street,
                City = city,
                StateProvince = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.StructuredPostal.Region))),
                PostalCode = postalCode,
                Country = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.StructuredPostal.Country))),
                Tag = MapAddressType(type)
            });
        }
    }

    private static void ReadNotes(ContentResolver resolver, string contactId, SharedContactData data)
    {
        var uri = ContactsContract.Data.ContentUri;
        var selection = $"{ContactsContract.Data.InterfaceConsts.ContactId} = ? AND {ContactsContract.Data.InterfaceConsts.Mimetype} = ?";
        var selectionArgs = new[] { contactId, ContactsContract.CommonDataKinds.Note.ContentItemType };

        using var cursor = resolver.Query(uri, null, selection, selectionArgs, null);
        if (cursor != null && cursor.MoveToFirst())
        {
            data.Notes = NullIfEmpty(cursor.GetString(cursor.GetColumnIndex(ContactsContract.CommonDataKinds.Note.InterfaceConsts.Data1)));
        }
    }

    private static int MapPhoneType(int type) => type switch
    {
        (int)PhoneDataKind.Mobile => 0,
        (int)PhoneDataKind.Home => 1,
        (int)PhoneDataKind.Work => 2,
        (int)PhoneDataKind.FaxWork or (int)PhoneDataKind.FaxHome => 3,
        _ => 99
    };

    private static int MapEmailType(int type) => type switch
    {
        (int)EmailDataKind.Work => 1,
        _ => 0
    };

    private static int MapAddressType(int type) => type switch
    {
        (int)AddressDataKind.Home => 0,
        (int)AddressDataKind.Work => 1,
        _ => 0
    };

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
