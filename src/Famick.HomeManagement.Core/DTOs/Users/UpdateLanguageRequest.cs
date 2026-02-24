namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Request to update user's preferred language only
/// </summary>
public class UpdateLanguageRequest
{
    /// <summary>
    /// Language code (e.g., "en", "es", "fr")
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;
}
