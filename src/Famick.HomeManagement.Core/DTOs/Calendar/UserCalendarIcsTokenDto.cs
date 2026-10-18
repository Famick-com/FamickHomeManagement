namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// DTO for a user's ICS feed token.
/// </summary>
public class UserCalendarIcsTokenDto
{
    public Guid Id { get; set; }
    /// <summary>
    /// The token itself. Set only in the response that creates it: afterwards only a hash exists.
    /// </summary>
    public string? Token { get; set; }
    public string? Label { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The full URL for the ICS feed using this token. Like <see cref="Token"/>, set only when
    /// the token is created.
    /// </summary>
    public string? FeedUrl { get; set; }
}
