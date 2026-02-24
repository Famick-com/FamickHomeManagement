namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Custom tag for categorizing contacts
/// </summary>
public class ContactTag : BaseTenantEntity
{
    /// <summary>
    /// Name of the tag
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the tag
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Color for the tag (hex format, e.g., "#FF5733")
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Icon name for the tag (optional)
    /// </summary>
    public string? Icon { get; set; }

    // Navigation properties
    public virtual ICollection<ContactTagLink> Contacts { get; set; } = new List<ContactTagLink>();
}
