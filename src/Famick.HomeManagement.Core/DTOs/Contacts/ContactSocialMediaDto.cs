using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Contact social media profile
/// </summary>
public class ContactSocialMediaDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }

    /// <summary>
    /// Social media service
    /// </summary>
    public SocialMediaService Service { get; set; }

    /// <summary>
    /// Username on the platform
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Full profile URL (optional)
    /// </summary>
    public string? ProfileUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}
