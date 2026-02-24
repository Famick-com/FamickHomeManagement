using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Social media profile associated with a contact
/// </summary>
public class ContactSocialMedia : BaseTenantEntity
{
    public Guid ContactId { get; set; }

    /// <summary>
    /// Social media platform
    /// </summary>
    public SocialMediaService Service { get; set; }

    /// <summary>
    /// Username or handle on the platform
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Full URL to the profile (optional)
    /// </summary>
    public string? ProfileUrl { get; set; }

    // Navigation properties
    public virtual Contact Contact { get; set; } = null!;
}
