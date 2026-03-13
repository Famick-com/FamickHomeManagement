namespace Famick.HomeManagement.Shared.Contacts;

/// <summary>
/// Represents a contact as it exists on the device (phone's native Contacts app).
/// Used as the canonical representation for hashing — both server data (via mapping)
/// and device-read data flow through this model before hashing.
/// </summary>
public class DeviceContactData
{
    public bool IsGroup { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Nickname { get; set; }
    public string? OrganizationName { get; set; }
    public string? JobTitle { get; set; }
    public string? Website { get; set; }
    public string? Notes { get; set; }
    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public List<DevicePhoneEntry> PhoneNumbers { get; set; } = new();
    public List<DeviceEmailEntry> EmailAddresses { get; set; } = new();
    public List<DeviceAddressEntry> Addresses { get; set; } = new();
    public List<DeviceSocialEntry> SocialProfiles { get; set; } = new();
}

public class DevicePhoneEntry
{
    public string PhoneNumber { get; set; } = string.Empty;
    public int Tag { get; set; } // 0=Mobile,1=Home,2=Work,3=Fax,99=Other
}

public class DeviceEmailEntry
{
    public string Email { get; set; } = string.Empty;
    public int Tag { get; set; } // 0=Personal/Home,1=Work,2=School,99=Other
}

public class DeviceAddressEntry
{
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public int Tag { get; set; } // 0=Home,1=Work,99=Other
}

public class DeviceSocialEntry
{
    public int Service { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? ProfileUrl { get; set; }
}
