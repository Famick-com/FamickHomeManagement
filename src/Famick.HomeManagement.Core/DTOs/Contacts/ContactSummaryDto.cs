using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Summary view of a contact for list displays
/// </summary>
public class ContactSummaryDto
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }
    public string? CompanyName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? GravatarUrl { get; set; }

    public ContactType? ContactType { get; set; }
    public Guid? ParentContactId { get; set; }
    public string? ParentGroupName { get; set; }
    public bool IsGroup { get; set; }

    /// <summary>
    /// Display name for the contact
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
    /// Primary email address if available
    /// </summary>
    public string? PrimaryEmail { get; set; }

    /// <summary>
    /// Primary phone number if available
    /// </summary>
    public string? PrimaryPhone { get; set; }

    /// <summary>
    /// Primary address display if available
    /// </summary>
    public string? PrimaryAddress { get; set; }

    /// <summary>
    /// Whether this contact is linked to a system user
    /// </summary>
    public bool IsUserLinked { get; set; }

    /// <summary>
    /// Tag names for display
    /// </summary>
    public List<string> TagNames { get; set; } = new();

    /// <summary>
    /// Tag colors for display
    /// </summary>
    public List<string?> TagColors { get; set; } = new();

    public ContactVisibilityLevel Visibility { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
