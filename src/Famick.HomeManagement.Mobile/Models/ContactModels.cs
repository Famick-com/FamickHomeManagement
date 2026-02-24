using System.ComponentModel;

namespace Famick.HomeManagement.Mobile.Models;

#region Response DTOs

public class ContactGroupSummaryDto
{
    public Guid Id { get; set; }
    public int ContactType { get; set; } // 0=Household, 1=Business
    public string GroupName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public int MemberCount { get; set; }
    public string? PrimaryAddress { get; set; }
    public bool IsTenantHousehold { get; set; }
    public List<string> TagNames { get; set; } = new();
    public List<string?> TagColors { get; set; } = new();
    public string? Website { get; set; }
    public string? BusinessCategory { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContactSummaryDto
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }
    public string? CompanyName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? GravatarUrl { get; set; }
    public int? ContactType { get; set; }
    public Guid? ParentContactId { get; set; }
    public string? ParentGroupName { get; set; }
    public bool IsGroup { get; set; }
    public string? DisplayName { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? PrimaryAddress { get; set; }
    public bool IsUserLinked { get; set; }
    public List<string> TagNames { get; set; } = new();
    public List<string?> TagColors { get; set; } = new();
    public int Visibility { get; set; } // 0=TenantShared, 1=UserPrivate, 2=SharedWithUsers
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class ContactDetailDto
{
    // Name
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }
    public string? DisplayName { get; set; }
    public string? FullName { get; set; }

    // Company
    public string? CompanyName { get; set; }
    public string? Title { get; set; }

    // Group fields
    public int? ContactType { get; set; }
    public Guid? ParentContactId { get; set; }
    public string? ParentGroupName { get; set; }
    public bool IsTenantHousehold { get; set; }
    public bool UsesGroupAddress { get; set; }
    public string? Website { get; set; }
    public string? BusinessCategory { get; set; }
    public bool IsGroup { get; set; }
    public List<ContactSummaryDto>? Members { get; set; }

    // Profile Image
    public string? ProfileImageFileName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? GravatarUrl { get; set; }
    public bool UseGravatar { get; set; } = true;

    // Demographics
    public int Gender { get; set; } // 0=Unknown,1=Male,2=Female,3=NonBinary,4=PreferNotToSay

    // Birth Date
    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public int BirthDatePrecision { get; set; } // 0=Unknown,1=Year,2=YearMonth,3=Full
    public string? FormattedBirthDate { get; set; }
    public int? Age { get; set; }

    // Death Date
    public int? DeathYear { get; set; }
    public int? DeathMonth { get; set; }
    public int? DeathDay { get; set; }
    public int DeathDatePrecision { get; set; }
    public string? FormattedDeathDate { get; set; }
    public bool IsDeceased { get; set; }

    // Notes
    public string? Notes { get; set; }

    // User Link
    public Guid? LinkedUserId { get; set; }
    public string? LinkedUserName { get; set; }
    public bool UsesTenantAddress { get; set; }

    // Ownership & Visibility
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public int Visibility { get; set; }
    public bool IsActive { get; set; } = true;

    // Related Data
    public List<ContactAddressDto> Addresses { get; set; } = new();
    public List<ContactPhoneNumberDto> PhoneNumbers { get; set; } = new();
    public List<ContactEmailAddressDto> EmailAddresses { get; set; } = new();
    public List<ContactSocialMediaDto> SocialMedia { get; set; } = new();
    public List<ContactRelationshipDto> Relationships { get; set; } = new();
    public List<ContactTagDto> Tags { get; set; } = new();
    public List<ContactUserShareDto> SharedWithUsers { get; set; } = new();

    public string? PrimaryEmail { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ContactAddressDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public Guid AddressId { get; set; }
    public AddressDto Address { get; set; } = null!;
    public int Tag { get; set; } // AddressTag: 0=Home,1=Work,2=School,3=Previous,4=Vacation,99=Other
    public bool IsPrimary { get; set; }
    public bool IsTenantAddress { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TagLabel => Tag switch
    {
        0 => "Home",
        1 => "Work",
        2 => "School",
        3 => "Previous",
        4 => "Vacation",
        _ => "Other"
    };
}

public class AddressDto
{
    public Guid Id { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
    public string? AddressLine4 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GeoapifyPlaceId { get; set; }
    public string? FormattedAddress { get; set; }

    public string DisplayAddress
    {
        get
        {
            if (!string.IsNullOrEmpty(FormattedAddress)) return FormattedAddress;
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

public class ContactPhoneNumberDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? NormalizedNumber { get; set; }
    public int Tag { get; set; } // PhoneTag: 0=Mobile,1=Home,2=Work,3=Fax,99=Other
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TagLabel => Tag switch
    {
        0 => "Mobile",
        1 => "Home",
        2 => "Work",
        3 => "Fax",
        _ => "Other"
    };
}

public class ContactEmailAddressDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? NormalizedEmail { get; set; }
    public int Tag { get; set; } // EmailTag: 0=Personal,1=Work,2=School,99=Other
    public bool IsPrimary { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TagLabel => Tag switch
    {
        0 => "Personal",
        1 => "Work",
        2 => "School",
        _ => "Other"
    };
}

public class ContactSocialMediaDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public int Service { get; set; } // SocialMediaService enum as int
    public string Username { get; set; } = string.Empty;
    public string? ProfileUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public string ServiceLabel => Service switch
    {
        1 => "Facebook",
        2 => "Twitter/X",
        3 => "Instagram",
        4 => "LinkedIn",
        5 => "TikTok",
        6 => "YouTube",
        7 => "Snapchat",
        8 => "Discord",
        9 => "WhatsApp",
        10 => "Telegram",
        11 => "BlueSky",
        12 => "Mastodon",
        13 => "Threads",
        14 => "Pinterest",
        15 => "Reddit",
        16 => "GitHub",
        _ => "Other"
    };
}

public class ContactRelationshipDto
{
    public Guid Id { get; set; }
    public Guid SourceContactId { get; set; }
    public Guid TargetContactId { get; set; }
    public int RelationshipType { get; set; } // RelationshipType enum as int
    public string? CustomLabel { get; set; }
    public string TargetContactName { get; set; } = string.Empty;
    public bool TargetIsUserLinked { get; set; }
    public string? DisplayText { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TypeLabel => RelationshipType switch
    {
        1 => "Mother", 2 => "Father", 3 => "Parent",
        4 => "Daughter", 5 => "Son", 6 => "Child",
        10 => "Sister", 11 => "Brother", 12 => "Sibling",
        20 => "Grandmother", 21 => "Grandfather", 22 => "Grandparent",
        23 => "Granddaughter", 24 => "Grandson", 25 => "Grandchild",
        30 => "Aunt", 31 => "Uncle", 32 => "Niece", 33 => "Nephew", 34 => "Cousin",
        40 => "Mother-in-Law", 41 => "Father-in-Law",
        42 => "Sister-in-Law", 43 => "Brother-in-Law",
        44 => "Daughter-in-Law", 45 => "Son-in-Law", 46 => "Sibling-in-Law",
        50 => "Spouse", 51 => "Partner", 52 => "Ex-Spouse", 53 => "Ex-Partner",
        60 => "Stepmother", 61 => "Stepfather", 62 => "Stepparent",
        63 => "Stepdaughter", 64 => "Stepson", 65 => "Stepchild",
        66 => "Stepsister", 67 => "Stepbrother", 68 => "Stepsibling",
        70 => "Colleague", 71 => "Boss", 72 => "Manager",
        73 => "Employee", 74 => "Client", 75 => "Vendor",
        80 => "Friend", 81 => "Neighbor", 82 => "Roommate", 83 => "Classmate",
        _ => CustomLabel ?? "Other"
    };
}

public class ContactTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public int ContactCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ContactUserShareDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public Guid SharedWithUserId { get; set; }
    public string SharedWithUserName { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContactAuditLogDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Action { get; set; } // ContactAuditAction enum as int
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }

    public string ActionLabel => Action switch
    {
        1 => "Created", 2 => "Updated", 3 => "Deleted", 4 => "Restored",
        10 => "Address Added", 11 => "Address Removed", 12 => "Address Updated", 13 => "Primary Address Changed",
        20 => "Phone Added", 21 => "Phone Removed", 22 => "Phone Updated", 23 => "Primary Phone Changed",
        30 => "Social Media Added", 31 => "Social Media Removed", 32 => "Social Media Updated",
        40 => "Relationship Added", 41 => "Relationship Removed", 42 => "Relationship Updated",
        50 => "Tag Added", 51 => "Tag Removed",
        60 => "Visibility Changed", 61 => "Shared", 62 => "Unshared",
        70 => "Email Added", 71 => "Email Removed", 72 => "Email Updated", 73 => "Primary Email Changed",
        80 => "Profile Image Updated", 81 => "Profile Image Removed",
        _ => "Unknown"
    };
}

public class PagedContactResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

#endregion

#region Request DTOs

public class ContactFilterRequest
{
    public string? SearchTerm { get; set; }
    public int? Visibility { get; set; }
    public List<Guid>? TagIds { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsUserLinked { get; set; }
    public int? ContactType { get; set; }
    public Guid? ParentContactId { get; set; }
    public bool? IsGroup { get; set; }
    public string SortBy { get; set; } = "LastName";
    public bool SortDescending { get; set; } = false;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class CreateContactRequest
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }
    public string? CompanyName { get; set; }
    public string? Title { get; set; }
    public int Gender { get; set; }
    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public int BirthDatePrecision { get; set; }
    public string? Notes { get; set; }
    public int Visibility { get; set; }
    public List<Guid>? TagIds { get; set; }
    public bool UseGravatar { get; set; } = true;
    public Guid? ParentContactId { get; set; }
}

public class UpdateContactRequest
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }
    public string? CompanyName { get; set; }
    public string? Title { get; set; }
    public int Gender { get; set; }
    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public int BirthDatePrecision { get; set; }
    public int? DeathYear { get; set; }
    public int? DeathMonth { get; set; }
    public int? DeathDay { get; set; }
    public int DeathDatePrecision { get; set; }
    public string? Notes { get; set; }
    public int Visibility { get; set; }
    public bool IsActive { get; set; } = true;
    public bool UseGravatar { get; set; } = true;
}

public class CreateContactGroupRequest
{
    public int ContactType { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Website { get; set; }
    public string? BusinessCategory { get; set; }
    public List<Guid>? TagIds { get; set; }
}

public class UpdateContactGroupRequest
{
    public int ContactType { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Website { get; set; }
    public string? BusinessCategory { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AddPhoneRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public int Tag { get; set; }
    public bool IsPrimary { get; set; }
}

public class AddEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public int Tag { get; set; }
    public bool IsPrimary { get; set; }
    public string? Label { get; set; }
}

public class AddContactAddressRequest
{
    public Guid? AddressId { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
    public string? AddressLine4 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GeoapifyPlaceId { get; set; }
    public string? FormattedAddress { get; set; }
    public int Tag { get; set; }
    public bool IsPrimary { get; set; }
}

public class AddSocialMediaRequest
{
    public int Service { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? ProfileUrl { get; set; }
}

public class AddRelationshipRequest
{
    public Guid TargetContactId { get; set; }
    public int RelationshipType { get; set; }
    public string? CustomLabel { get; set; }
    public bool CreateInverse { get; set; } = true;
}

public class ShareContactRequest
{
    public Guid SharedWithUserId { get; set; }
    public bool CanEdit { get; set; }
}

public class CreateContactTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
}

public class UpdateContactTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
}

#endregion

#region Display Models

public class ContactGroupDisplayModel : INotifyPropertyChanged
{
    private readonly ContactGroupSummaryDto _dto;
    private ImageSource? _profileImageSource;

    public ContactGroupDisplayModel(ContactGroupSummaryDto dto)
    {
        _dto = dto;
    }

    public Guid Id => _dto.Id;
    public string GroupName => _dto.GroupName;
    public int ContactType => _dto.ContactType;
    public int MemberCount => _dto.MemberCount;
    public string? PrimaryAddress => _dto.PrimaryAddress;
    public bool IsTenantHousehold => _dto.IsTenantHousehold;
    public List<string> TagNames => _dto.TagNames;
    public List<string?> TagColors => _dto.TagColors;
    public string? Website => _dto.Website;
    public string? BusinessCategory => _dto.BusinessCategory;
    public string? ProfileImageUrl => _dto.ProfileImageUrl;

    public string TypeLabel => ContactType == 0 ? "Household" : "Business";

    public string Initials
    {
        get
        {
            var words = GroupName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
                return $"{words[0][0]}{words[1][0]}".ToUpper();
            return words.Length == 1 && words[0].Length > 0
                ? words[0][0].ToString().ToUpper()
                : "?";
        }
    }

    public Color BackgroundColor => ContactType == 0
        ? Color.FromArgb("#4CAF50")
        : Color.FromArgb("#2196F3");

    public string MemberCountText => MemberCount == 1 ? "1 member" : $"{MemberCount} members";

    [System.Text.Json.Serialization.JsonIgnore]
    public ImageSource? ProfileImageSource
    {
        get => _profileImageSource;
        set
        {
            _profileImageSource = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProfileImageSource)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class ContactDisplayModel : INotifyPropertyChanged
{
    private readonly ContactSummaryDto _dto;
    private ImageSource? _thumbnailSource;

    public ContactDisplayModel(ContactSummaryDto dto)
    {
        _dto = dto;
    }

    public Guid Id => _dto.Id;
    public string DisplayName => _dto.DisplayName ?? $"{_dto.FirstName} {_dto.LastName}".Trim();
    public string? FirstName => _dto.FirstName;
    public string? LastName => _dto.LastName;
    public string? CompanyName => _dto.CompanyName;
    public string? PrimaryEmail => _dto.PrimaryEmail;
    public string? PrimaryPhone => _dto.PrimaryPhone;
    public string? PrimaryAddress => _dto.PrimaryAddress;
    public string? ProfileImageUrl => _dto.ProfileImageUrl;
    public string? GravatarUrl => _dto.GravatarUrl;
    public bool IsUserLinked => _dto.IsUserLinked;
    public string? ParentGroupName => _dto.ParentGroupName;
    public List<string> TagNames => _dto.TagNames;
    public List<string?> TagColors => _dto.TagColors;

    public string Initials
    {
        get
        {
            var first = _dto.FirstName;
            var last = _dto.LastName;
            if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(last))
                return $"{first[0]}{last[0]}".ToUpper();
            if (!string.IsNullOrEmpty(first))
                return first[0].ToString().ToUpper();
            if (!string.IsNullOrEmpty(last))
                return last[0].ToString().ToUpper();
            return "?";
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public ImageSource? ThumbnailSource
    {
        get => _thumbnailSource;
        set
        {
            _thumbnailSource = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailSource)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

#endregion

#region Popup Result Records

public sealed record AddPhoneResult(string PhoneNumber, int Tag, bool IsPrimary);
public sealed record AddEmailResult(string Email, int Tag, bool IsPrimary, string? Label);
public sealed record AddAddressResult(
    string? AddressLine1, string? AddressLine2, string? City,
    string? StateProvince, string? PostalCode, string? Country,
    int Tag, bool IsPrimary, Guid? AddressId = null);
public sealed record AddSocialMediaResult(int Service, string Username, string? ProfileUrl);
public sealed record AddRelationshipResult(Guid TargetContactId, string TargetContactName, int RelationshipType, string? CustomLabel, bool CreateInverse);
public sealed record MoveToGroupResult(Guid GroupId, string GroupName);
public sealed record ShareContactResult(Guid UserId, string UserName, bool CanEdit);

#endregion
