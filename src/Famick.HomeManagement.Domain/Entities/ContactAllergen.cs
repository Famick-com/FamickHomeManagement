using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Tracks a specific allergen for a household member contact.
/// </summary>
public class ContactAllergen : BaseEntity
{
    public Guid ContactId { get; set; }

    public AllergenType AllergenType { get; set; }

    public AllergenSeverity Severity { get; set; }

    // Navigation properties
    public virtual Contact Contact { get; set; } = null!;
}
