using System.Security.Cryptography;
using System.Text;

namespace Famick.HomeManagement.Shared.Contacts;

/// <summary>
/// Single source of truth for contact hash computation.
/// ALL contact hashing flows through BuildDeviceFieldsString → SHA256.
///
/// Two hash variants:
/// - ComputeHashWithPhotos: includes photo identity (server→device change detection)
/// - ComputeHash: excludes photos (device→server change detection)
/// Both share the same base string via BuildDeviceFieldsString.
/// </summary>
public static class ContactHasher
{
    /// <summary>
    /// Computes a hash from device contact data, excluding photo fields.
    /// This is the primary hash for device→server change detection.
    /// </summary>
    public static string ComputeHash(DeviceContactData device)
    {
        var sb = BuildDeviceFieldsString(device);
        sb.Append("|IMG:");
        sb.Append("|GRAV:");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Computes a hash from device contact data with photo identity appended.
    /// Used for server→device change detection where photo changes matter.
    /// </summary>
    public static string ComputeHashWithPhotos(
        DeviceContactData device,
        string? profileImageFileName,
        bool useGravatar,
        string? gravatarUrl)
    {
        var sb = BuildDeviceFieldsString(device);
        sb.Append($"|IMG:{profileImageFileName}");
        sb.Append($"|GRAV:{(useGravatar && gravatarUrl != null ? gravatarUrl : "")}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Builds the canonical hash string from device contact data. This is the single
    /// function that ALL contact hashes flow through.
    /// </summary>
    internal static StringBuilder BuildDeviceFieldsString(DeviceContactData device)
    {
        var sb = new StringBuilder();
        sb.Append("v7|");
        sb.Append(device.IsGroup);
        sb.Append('|');
        sb.Append(device.IsGroup ? (device.DisplayName ?? device.OrganizationName) : null);
        sb.Append('|');
        sb.Append(device.FirstName);
        sb.Append('|');
        sb.Append(device.MiddleName);
        sb.Append('|');
        sb.Append(device.LastName);
        sb.Append('|');
        sb.Append(device.Nickname);
        sb.Append('|');
        sb.Append(device.OrganizationName);
        sb.Append('|');
        sb.Append(device.JobTitle);
        sb.Append('|');
        sb.Append(device.Website);
        sb.Append('|');
        // Notes intentionally excluded: iOS cannot read Notes (missing entitlement),
        // so including it would cause false positives on every sync.
        sb.Append('|');
        sb.Append(device.BirthYear);
        sb.Append('|');
        sb.Append(device.BirthMonth);
        sb.Append('|');
        sb.Append(device.BirthDay);

        foreach (var phone in device.PhoneNumbers.OrderBy(p => p.PhoneNumber).ThenBy(p => p.Tag))
            sb.Append($"|P:{phone.PhoneNumber}:{phone.Tag}");

        foreach (var email in device.EmailAddresses.OrderBy(e => e.Email).ThenBy(e => e.Tag))
            sb.Append($"|E:{email.Email}:{email.Tag}");

        foreach (var addr in device.Addresses.OrderBy(a => a.AddressLine1).ThenBy(a => a.City).ThenBy(a => a.Tag))
            sb.Append($"|A:{addr.AddressLine1}:{addr.City}:{addr.StateProvince}:{addr.PostalCode}:{addr.Country}:{addr.Tag}");

        foreach (var social in device.SocialProfiles.OrderBy(s => s.Service).ThenBy(s => s.Username))
            sb.Append($"|S:{social.Service}:{social.Username}");

        return sb;
    }

    #region Tag Normalization (Device Round-Trip)

    /// <summary>Phone: 0=Mobile, 1=Home, 2=Work, 3=Fax survive round-trip; all others → 99</summary>
    public static int NormalizePhoneTag(int tag) => tag is >= 0 and <= 3 ? tag : 99;

    /// <summary>Email: 0=Personal/Home, 1=Work survive round-trip; School(2) and others → 99</summary>
    public static int NormalizeEmailTag(int tag) => tag is 0 or 1 ? tag : 99;

    /// <summary>Address: 0=Home, 1=Work survive round-trip; School/Previous/Vacation → 99</summary>
    public static int NormalizeAddressTag(int tag) => tag is 0 or 1 ? tag : 99;

    /// <summary>Social: Facebook(1), Twitter(2), Instagram(3), LinkedIn(4) survive; others → 0</summary>
    public static int NormalizeSocialService(int service) => service is >= 1 and <= 4 ? service : 0;

    #endregion
}
