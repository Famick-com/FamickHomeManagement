namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// DTO for a user's vCard feed token.
/// </summary>
public class UserContactVcfTokenDto
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The full URL for the vCard feed using this token.
    /// Computed at the controller level based on the request URL.
    /// </summary>
    public string? FeedUrl { get; set; }
}
