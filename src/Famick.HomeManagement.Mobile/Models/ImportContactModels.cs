namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// Contact data parsed from a shared vCard file, used as input to the import screen.
/// </summary>
public class SharedContactData
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public List<SharedPhoneEntry> PhoneNumbers { get; set; } = new();
    public List<SharedEmailEntry> EmailAddresses { get; set; } = new();
    public List<SharedAddressEntry> Addresses { get; set; } = new();

    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(FirstName)) parts.Add(FirstName.Trim());
            if (!string.IsNullOrWhiteSpace(MiddleName)) parts.Add(MiddleName.Trim());
            if (!string.IsNullOrWhiteSpace(LastName)) parts.Add(LastName.Trim());
            if (parts.Count > 0) return string.Join(" ", parts);
            return CompanyName?.Trim() ?? "Unknown Contact";
        }
    }

    public bool HasAddress => Addresses.Count > 0;
    public bool HasPhone => PhoneNumbers.Count > 0;
    public bool HasEmail => EmailAddresses.Count > 0;
    public bool HasCompany => !string.IsNullOrWhiteSpace(CompanyName);
    public bool HasBirthday => BirthYear.HasValue || BirthMonth.HasValue || BirthDay.HasValue;
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
}

public class SharedPhoneEntry
{
    public string PhoneNumber { get; set; } = string.Empty;
    public int Tag { get; set; } // PhoneTag: 0=Mobile, 1=Home, 2=Work, 3=Fax, 99=Other
    public bool IsSelected { get; set; } = true;

    public string TagLabel => Tag switch
    {
        0 => "Mobile",
        1 => "Home",
        2 => "Work",
        3 => "Fax",
        _ => "Other"
    };
}

public class SharedEmailEntry
{
    public string Email { get; set; } = string.Empty;
    public int Tag { get; set; } // EmailTag: 0=Personal, 1=Work, 2=School, 99=Other
    public bool IsSelected { get; set; } = true;

    public string TagLabel => Tag switch
    {
        0 => "Personal",
        1 => "Work",
        2 => "School",
        _ => "Other"
    };
}

public class SharedAddressEntry
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public int Tag { get; set; } // AddressTag: 0=Home, 1=Work, 2=School, 3=Previous, 4=Vacation, 99=Other
    public bool IsSelected { get; set; } = true;

    public NormalizedAddressResult? ValidatedAddress { get; set; }
    public bool IsValidated { get; set; }

    public string TagLabel => Tag switch
    {
        0 => "Home",
        1 => "Work",
        2 => "School",
        3 => "Previous",
        4 => "Vacation",
        _ => "Other"
    };

    public string DisplayAddress
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(AddressLine1)) parts.Add(AddressLine1);
            if (!string.IsNullOrEmpty(AddressLine2)) parts.Add(AddressLine2);
            var cityState = string.Join(", ",
                new[] { City, StateProvince }.Where(s => !string.IsNullOrEmpty(s)));
            if (!string.IsNullOrEmpty(cityState))
            {
                if (!string.IsNullOrEmpty(PostalCode))
                    cityState += " " + PostalCode;
                parts.Add(cityState);
            }
            if (!string.IsNullOrEmpty(Country)) parts.Add(Country);
            return string.Join("\n", parts);
        }
    }
}

public record HouseholdMatch(
    Guid GroupId,
    string GroupName,
    bool IsNameMatch,
    bool IsAddressMatch);

public record ContactMatch(
    Guid ContactId,
    string DisplayName,
    Guid HouseholdId,
    string HouseholdName);
