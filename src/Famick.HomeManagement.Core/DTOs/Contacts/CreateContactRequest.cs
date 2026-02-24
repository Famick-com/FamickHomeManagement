using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to create a new contact
/// </summary>
public class CreateContactRequest
{
    #region Name

    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }

    #endregion

    #region Company

    public string? CompanyName { get; set; }
    public string? Title { get; set; }

    #endregion

    #region Demographics

    public Gender Gender { get; set; } = Gender.Unknown;

    #endregion

    #region Birth Date

    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public DatePrecision BirthDatePrecision { get; set; } = DatePrecision.Unknown;

    #endregion

    #region Death Date

    public int? DeathYear { get; set; }
    public int? DeathMonth { get; set; }
    public int? DeathDay { get; set; }
    public DatePrecision DeathDatePrecision { get; set; } = DatePrecision.Unknown;

    #endregion

    #region Notes

    public string? Notes { get; set; }

    #endregion

    #region Visibility

    public ContactVisibilityLevel Visibility { get; set; } = ContactVisibilityLevel.TenantShared;

    #endregion

    #region Initial Tags (optional)

    public List<Guid>? TagIds { get; set; }

    #endregion

    #region Gravatar

    public bool UseGravatar { get; set; } = true;

    #endregion

    #region Group Membership

    /// <summary>
    /// Parent group to add this contact to. Defaults to tenant household if null.
    /// </summary>
    public Guid? ParentContactId { get; set; }

    #endregion
}
