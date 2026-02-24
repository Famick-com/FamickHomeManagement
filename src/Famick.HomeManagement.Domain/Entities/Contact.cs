using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a contact in the system. Contacts can be linked to users,
/// shared across the tenant, or kept private.
/// </summary>
public class Contact : BaseTenantEntity
{
    // Name (nullable - either name or company required)
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }

    // Company information
    public string? CompanyName { get; set; }
    public string? Title { get; set; }

    // Profile image
    public string? ProfileImageFileName { get; set; }

    /// <summary>
    /// Whether to use Gravatar for this contact's profile image when no custom image is uploaded
    /// </summary>
    public bool UseGravatar { get; set; } = true;

    // Demographics
    public Gender Gender { get; set; } = Gender.Unknown;

    // Birth Date (nullable components for partial date support)
    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public DatePrecision BirthDatePrecision { get; set; } = DatePrecision.Unknown;

    // Death Date (nullable components for partial date support)
    public int? DeathYear { get; set; }
    public int? DeathMonth { get; set; }
    public int? DeathDay { get; set; }
    public DatePrecision DeathDatePrecision { get; set; } = DatePrecision.Unknown;

    public string? Notes { get; set; }

    // Contact Group hierarchy
    /// <summary>
    /// Type of contact group (Household or Business). Set on group contacts, null on members.
    /// </summary>
    public ContactType? ContactType { get; set; }

    /// <summary>
    /// Parent group contact ID. Null = this is a group contact, set = this is a member.
    /// </summary>
    public Guid? ParentContactId { get; set; }

    /// <summary>
    /// Marks the special tenant household group (one per tenant)
    /// </summary>
    public bool IsTenantHousehold { get; set; }

    /// <summary>
    /// When true, this member uses the parent group's address
    /// </summary>
    public bool UsesGroupAddress { get; set; }

    /// <summary>
    /// Website URL (Business groups only)
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Business category (Business groups only)
    /// </summary>
    public string? BusinessCategory { get; set; }

    /// <summary>
    /// Reference to another tenant if this contact belongs to a different household
    /// </summary>
    public Guid? HouseholdTenantId { get; set; }

    /// <summary>
    /// Link to User entity if this contact represents a system user
    /// </summary>
    public Guid? LinkedUserId { get; set; }

    /// <summary>
    /// When true, this contact's home address references the tenant's address
    /// </summary>
    public bool UsesTenantAddress { get; set; } = false;

    /// <summary>
    /// User who created this contact
    /// </summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// Visibility level for this contact
    /// </summary>
    public ContactVisibilityLevel Visibility { get; set; } = ContactVisibilityLevel.TenantShared;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Contact? ParentContact { get; set; }
    public virtual ICollection<Contact> Members { get; set; } = new List<Contact>();
    public virtual User? LinkedUser { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<ContactAddress> Addresses { get; set; } = new List<ContactAddress>();
    public virtual ICollection<ContactPhoneNumber> PhoneNumbers { get; set; } = new List<ContactPhoneNumber>();
    public virtual ICollection<ContactEmailAddress> EmailAddresses { get; set; } = new List<ContactEmailAddress>();
    public virtual ICollection<ContactSocialMedia> SocialMedia { get; set; } = new List<ContactSocialMedia>();
    public virtual ICollection<ContactRelationship> RelationshipsAsSource { get; set; } = new List<ContactRelationship>();
    public virtual ICollection<ContactRelationship> RelationshipsAsTarget { get; set; } = new List<ContactRelationship>();
    public virtual ICollection<ContactTagLink> Tags { get; set; } = new List<ContactTagLink>();
    public virtual ICollection<ContactUserShare> SharedWithUsers { get; set; } = new List<ContactUserShare>();
    public virtual ICollection<ContactAuditLog> AuditLogs { get; set; } = new List<ContactAuditLog>();

    /// <summary>
    /// Whether this contact is a group (has no parent)
    /// </summary>
    public bool IsGroup => ParentContactId == null;

    /// <summary>
    /// Gets the display name for this contact.
    /// Returns PreferredName if set, otherwise name, otherwise company name.
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
    /// Gets the full name for this contact (FirstName MiddleName LastName)
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
}
