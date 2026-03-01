using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Tracks a dietary preference for a household member contact.
/// </summary>
public class ContactDietaryPreference : BaseEntity
{
    public Guid ContactId { get; set; }

    public DietaryPreference DietaryPreference { get; set; }

    // Navigation properties
    public virtual Contact Contact { get; set; } = null!;
}
