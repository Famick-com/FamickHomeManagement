namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Tracks which users a contact is shared with when visibility is SharedWithUsers
/// </summary>
public class ContactUserShare : BaseTenantEntity
{
    public Guid ContactId { get; set; }

    /// <summary>
    /// User this contact is shared with
    /// </summary>
    public Guid SharedWithUserId { get; set; }

    /// <summary>
    /// Whether the shared user can edit the contact
    /// </summary>
    public bool CanEdit { get; set; } = false;

    // Navigation properties
    public virtual Contact Contact { get; set; } = null!;
    public virtual User SharedWithUser { get; set; } = null!;
}
