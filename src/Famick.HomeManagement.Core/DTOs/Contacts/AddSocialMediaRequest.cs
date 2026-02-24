using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to add a social media profile to a contact
/// </summary>
public class AddSocialMediaRequest
{
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
}
