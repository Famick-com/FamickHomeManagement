using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Email address associated with a contact
/// </summary>
public class ContactEmailAddress : BaseTenantEntity
{
    public Guid ContactId { get; set; }

    /// <summary>
    /// Email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Normalized email for comparison (lowercase, trimmed)
    /// </summary>
    public string? NormalizedEmail { get; set; }

    /// <summary>
    /// Type of email address (personal, work, etc.)
    /// </summary>
    public EmailTag Tag { get; set; } = EmailTag.Personal;

    /// <summary>
    /// Whether this is the primary email for the contact
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// Optional label for custom descriptions
    /// </summary>
    public string? Label { get; set; }

    // Navigation properties
    public virtual Contact Contact { get; set; } = null!;
}
