using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Full contact data transfer object with all details
/// </summary>
public class ContactDto
{
    public Guid Id { get; set; }

    #region Name

    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }

    /// <summary>
    /// Display name for the contact (PreferredName or name or company)
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PreferredName))
                return PreferredName;

            var name = $"{FirstName} {LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            return CompanyName ?? "Unknown";
        }
    }

    /// <summary>
    /// Full name including middle name
    /// </summary>
    public string FullName
    {
        get
        {
            var parts = new[] { FirstName, MiddleName, LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(" ", parts);
        }
    }

    #endregion

    #region Company

    public string? CompanyName { get; set; }
    public string? Title { get; set; }

    #endregion

    #region Contact Group

    public ContactType? ContactType { get; set; }
    public Guid? ParentContactId { get; set; }
    public string? ParentGroupName { get; set; }
    public bool IsTenantHousehold { get; set; }
    public bool UsesGroupAddress { get; set; }
    public string? Website { get; set; }
    public string? BusinessCategory { get; set; }
    public bool IsGroup { get; set; }
    public List<ContactSummaryDto>? Members { get; set; }

    #endregion

    #region Profile Image

    public string? ProfileImageFileName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? GravatarUrl { get; set; }
    public bool UseGravatar { get; set; } = true;

    #endregion

    #region Demographics

    public Gender Gender { get; set; } = Gender.Unknown;

    #endregion

    #region Birth Date

    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public DatePrecision BirthDatePrecision { get; set; } = DatePrecision.Unknown;

    /// <summary>
    /// Formatted birth date based on precision
    /// </summary>
    public string? FormattedBirthDate => FormatDate(BirthYear, BirthMonth, BirthDay, BirthDatePrecision);

    /// <summary>
    /// Calculated age if birth date is known
    /// </summary>
    public int? Age => CalculateAge(BirthYear, BirthMonth, BirthDay);

    #endregion

    #region Death Date

    public int? DeathYear { get; set; }
    public int? DeathMonth { get; set; }
    public int? DeathDay { get; set; }
    public DatePrecision DeathDatePrecision { get; set; } = DatePrecision.Unknown;

    /// <summary>
    /// Formatted death date based on precision
    /// </summary>
    public string? FormattedDeathDate => FormatDate(DeathYear, DeathMonth, DeathDay, DeathDatePrecision);

    /// <summary>
    /// Whether this contact is marked as deceased
    /// </summary>
    public bool IsDeceased => DeathYear.HasValue;

    #endregion

    #region Notes

    public string? Notes { get; set; }

    #endregion

    #region User Link

    /// <summary>
    /// If this contact is linked to a system user
    /// </summary>
    public Guid? LinkedUserId { get; set; }
    public string? LinkedUserName { get; set; }

    /// <summary>
    /// Whether the contact's home address is the tenant's address
    /// </summary>
    public bool UsesTenantAddress { get; set; }

    #endregion

    #region Ownership & Visibility

    public Guid CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public ContactVisibilityLevel Visibility { get; set; } = ContactVisibilityLevel.TenantShared;
    public bool IsActive { get; set; } = true;

    #endregion

    #region Related Data

    public List<ContactAddressDto> Addresses { get; set; } = new();
    public List<ContactPhoneNumberDto> PhoneNumbers { get; set; } = new();
    public List<ContactEmailAddressDto> EmailAddresses { get; set; } = new();
    public List<ContactSocialMediaDto> SocialMedia { get; set; } = new();
    public List<ContactRelationshipDto> Relationships { get; set; } = new();
    public List<ContactTagDto> Tags { get; set; } = new();
    public List<ContactUserShareDto> SharedWithUsers { get; set; } = new();

    /// <summary>
    /// Primary email address (convenience property)
    /// </summary>
    public string? PrimaryEmail => EmailAddresses
        .Where(e => e.IsPrimary)
        .Select(e => e.Email)
        .FirstOrDefault() ?? EmailAddresses
        .OrderBy(e => e.CreatedAt)
        .Select(e => e.Email)
        .FirstOrDefault();

    #endregion

    #region Audit

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    #endregion

    #region Helper Methods

    private static string? FormatDate(int? year, int? month, int? day, DatePrecision precision)
    {
        return precision switch
        {
            DatePrecision.Full when year.HasValue && month.HasValue && day.HasValue =>
                new DateTime(year.Value, month.Value, day.Value).ToString("MMMM d, yyyy"),
            DatePrecision.YearMonth when year.HasValue && month.HasValue =>
                new DateTime(year.Value, month.Value, 1).ToString("MMMM yyyy"),
            DatePrecision.Year when year.HasValue =>
                year.Value.ToString(),
            _ => null
        };
    }

    private static int? CalculateAge(int? birthYear, int? birthMonth, int? birthDay)
    {
        if (!birthYear.HasValue) return null;

        var today = DateTime.Today;
        var birthDate = new DateTime(birthYear.Value, birthMonth ?? 1, birthDay ?? 1);
        var age = today.Year - birthDate.Year;

        if (birthDate.Date > today.AddYears(-age))
            age--;

        return age;
    }

    #endregion
}
