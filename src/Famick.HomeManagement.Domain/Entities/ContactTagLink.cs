namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Many-to-many link between contacts and tags
/// </summary>
public class ContactTagLink : BaseTenantEntity
{
    public Guid ContactId { get; set; }
    public Guid TagId { get; set; }

    // Navigation properties
    public virtual Contact Contact { get; set; } = null!;
    public virtual ContactTag Tag { get; set; } = null!;
}
