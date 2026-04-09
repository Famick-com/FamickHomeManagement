using Contacts;
using ContactsUI;
using Foundation;
using UIKit;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

public class DeviceContactPicker : IDeviceContactPicker
{
    public Task<SharedContactData?> PickContactAsync()
    {
        var tcs = new TaskCompletionSource<SharedContactData?>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var picker = new CNContactPickerViewController();
            var del = new ContactPickerDelegate(tcs);
            picker.Delegate = del;

            var vc = GetTopViewController();
            if (vc == null)
            {
                tcs.SetResult(null);
                return;
            }

            vc.PresentViewController(picker, true, null);
        });

        return tcs.Task;
    }

    private static UIViewController? GetTopViewController()
    {
        var scenes = UIApplication.SharedApplication.ConnectedScenes;
        foreach (var scene in scenes)
        {
            if (scene is UIWindowScene windowScene)
            {
                var window = windowScene.Windows.FirstOrDefault(w => w.IsKeyWindow);
                if (window?.RootViewController != null)
                {
                    var vc = window.RootViewController;
                    while (vc.PresentedViewController != null)
                        vc = vc.PresentedViewController;
                    return vc;
                }
            }
        }
        return null;
    }

    private class ContactPickerDelegate : CNContactPickerDelegate
    {
        private readonly TaskCompletionSource<SharedContactData?> _tcs;

        // Keys to fetch - must be requested upfront
        private static readonly NSString[] FetchKeys =
        {
            CNContactKey.GivenName,
            CNContactKey.MiddleName,
            CNContactKey.FamilyName,
            CNContactKey.OrganizationName,
            CNContactKey.JobTitle,
            CNContactKey.Note,
            CNContactKey.Birthday,
            CNContactKey.PhoneNumbers,
            CNContactKey.EmailAddresses,
            CNContactKey.PostalAddresses,
        };

        public ContactPickerDelegate(TaskCompletionSource<SharedContactData?> tcs) => _tcs = tcs;

        public override void ContactPickerDidCancel(CNContactPickerViewController picker)
        {
            picker.DismissViewController(true, null);
            _tcs.TrySetResult(null);
        }

        public override void DidSelectContact(CNContactPickerViewController picker, CNContact contact)
        {
            picker.DismissViewController(true, null);

            try
            {
                // Re-fetch with all keys since the picker may not provide all fields
                var store = new CNContactStore();
                var fullContact = store.GetUnifiedContact(contact.Identifier, FetchKeys, out var error);
                if (error != null || fullContact == null)
                {
                    // Fall back to what we got from the picker
                    _tcs.TrySetResult(MapContact(contact));
                    return;
                }

                _tcs.TrySetResult(MapContact(fullContact));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeviceContactPicker] Error mapping contact: {ex.Message}");
                _tcs.TrySetResult(MapContact(contact));
            }
        }

        private static SharedContactData MapContact(CNContact contact)
        {
            var data = new SharedContactData
            {
                FirstName = NullIfEmpty(contact.GivenName),
                MiddleName = NullIfEmpty(contact.MiddleName),
                LastName = NullIfEmpty(contact.FamilyName),
                CompanyName = NullIfEmpty(contact.OrganizationName),
                Title = NullIfEmpty(contact.JobTitle),
            };

            // Notes - may throw if key not available
            try { data.Notes = NullIfEmpty(contact.Note); } catch { }

            // Birthday
            try
            {
                if (contact.Birthday != null)
                {
                    var bday = contact.Birthday;
                    if (bday.Year > 0) data.BirthYear = (int)bday.Year;
                    if (bday.Month > 0) data.BirthMonth = (int)bday.Month;
                    if (bday.Day > 0) data.BirthDay = (int)bday.Day;
                }
            }
            catch { }

            // Phone numbers
            try
            {
                if (contact.PhoneNumbers != null)
                {
                    foreach (var phone in contact.PhoneNumbers)
                    {
                        var number = phone.Value?.StringValue;
                        if (string.IsNullOrWhiteSpace(number)) continue;

                        data.PhoneNumbers.Add(new SharedPhoneEntry
                        {
                            PhoneNumber = number,
                            Tag = MapPhoneTag(phone.Label)
                        });
                    }
                }
            }
            catch { }

            // Email addresses
            try
            {
                if (contact.EmailAddresses != null)
                {
                    foreach (var email in contact.EmailAddresses)
                    {
                        var address = email.Value?.ToString();
                        if (string.IsNullOrWhiteSpace(address)) continue;

                        data.EmailAddresses.Add(new SharedEmailEntry
                        {
                            Email = address,
                            Tag = MapEmailTag(email.Label)
                        });
                    }
                }
            }
            catch { }

            // Postal addresses
            try
            {
                if (contact.PostalAddresses != null)
                {
                    foreach (var addr in contact.PostalAddresses)
                    {
                        var postal = addr.Value;
                        if (postal == null) continue;

                        var street = NullIfEmpty(postal.Street);
                        var city = NullIfEmpty(postal.City);
                        var postalCode = NullIfEmpty(postal.PostalCode);

                        if (street == null && city == null && postalCode == null) continue;

                        data.Addresses.Add(new SharedAddressEntry
                        {
                            AddressLine1 = street,
                            City = city,
                            StateProvince = NullIfEmpty(postal.State),
                            PostalCode = postalCode,
                            Country = NullIfEmpty(postal.Country),
                            Tag = MapAddressTag(addr.Label)
                        });
                    }
                }
            }
            catch { }

            return data;
        }

        private static int MapPhoneTag(string? label)
        {
            if (label == null) return 99;
            if (label.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) return 0;
            if (label.Contains("Home", StringComparison.OrdinalIgnoreCase)) return 1;
            if (label.Contains("Work", StringComparison.OrdinalIgnoreCase)) return 2;
            if (label.Contains("Fax", StringComparison.OrdinalIgnoreCase)) return 3;
            return 99;
        }

        private static int MapEmailTag(string? label)
        {
            if (label == null) return 0;
            if (label.Contains("Work", StringComparison.OrdinalIgnoreCase)) return 1;
            if (label.Contains("School", StringComparison.OrdinalIgnoreCase)) return 2;
            return 0;
        }

        private static int MapAddressTag(string? label)
        {
            if (label == null) return 0;
            if (label.Contains("Home", StringComparison.OrdinalIgnoreCase)) return 0;
            if (label.Contains("Work", StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }

        private static string? NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
