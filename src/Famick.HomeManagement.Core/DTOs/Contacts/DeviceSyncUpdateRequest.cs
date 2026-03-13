namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Full-replacement DTO for pushing device contact edits to the server.
/// All collection fields replace existing data entirely (no partial updates).
/// </summary>
public class DeviceSyncUpdateRequest
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }
    public string? CompanyName { get; set; }
    public string? Title { get; set; }
    public string? Website { get; set; }
    public string? Notes { get; set; }
    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public List<DeviceSyncPhoneEntry> PhoneNumbers { get; set; } = new();
    public List<DeviceSyncEmailEntry> EmailAddresses { get; set; } = new();
    public List<DeviceSyncAddressEntry> Addresses { get; set; } = new();
    public List<DeviceSyncSocialEntry> SocialMedia { get; set; } = new();
}

public class DeviceSyncPhoneEntry
{
    public string PhoneNumber { get; set; } = string.Empty;
    public int Tag { get; set; }
}

public class DeviceSyncEmailEntry
{
    public string Email { get; set; } = string.Empty;
    public int Tag { get; set; }
}

public class DeviceSyncAddressEntry
{
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public int Tag { get; set; }
}

public class DeviceSyncSocialEntry
{
    public int Service { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? ProfileUrl { get; set; }
}
