using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Phone number associated with a contact
/// </summary>
public class ContactPhoneNumber : BaseTenantEntity
{
    public Guid ContactId { get; set; }

    /// <summary>
    /// Phone number (stored as entered, may include formatting)
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Normalized phone number for comparison (digits only)
    /// </summary>
    public string? NormalizedNumber { get; set; }

    /// <summary>
    /// Type of phone number (mobile, home, work, etc.)
    /// </summary>
    public PhoneTag Tag { get; set; } = PhoneTag.Mobile;

    /// <summary>
    /// Whether this is the primary phone number for the contact
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// Optional label for custom descriptions
    /// </summary>
    public string? Label { get; set; }

    // Navigation properties
    public virtual Contact Contact { get; set; } = null!;
}
