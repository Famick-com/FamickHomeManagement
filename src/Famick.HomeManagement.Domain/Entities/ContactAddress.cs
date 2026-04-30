using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Links a contact to an address with a tag (home, work, etc.)
/// </summary>
public class ContactAddress : BaseTenantEntity
{
    public Guid ContactId { get; set; }
    public Guid AddressId { get; set; }

    /// <summary>
    /// Type of address (home, work, etc.)
    /// </summary>
    public AddressTag Tag { get; set; } = AddressTag.Home;

    /// <summary>
    /// Whether this is the primary address for the contact
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// Optional label for custom descriptions
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Per-contact apt / suite / unit number. The shared <see cref="Address"/>
    /// row represents the building (one row per Line1+City+State+Postal+Country);
    /// the unit a specific contact lives in is stored here so two contacts in
    /// different units of the same building don't collapse into one row.
    /// </summary>
    public string? AddressLine2 { get; set; }

    // Navigation properties
    public virtual Contact Contact { get; set; } = null!;
    public virtual Address Address { get; set; } = null!;
}
