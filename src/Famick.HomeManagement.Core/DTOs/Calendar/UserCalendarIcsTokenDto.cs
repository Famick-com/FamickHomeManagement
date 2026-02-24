namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// DTO for a user's ICS feed token.
/// </summary>
public class UserCalendarIcsTokenDto
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The full URL for the ICS feed using this token.
    /// Computed at the controller level based on the request URL.
    /// </summary>
    public string? FeedUrl { get; set; }
}
