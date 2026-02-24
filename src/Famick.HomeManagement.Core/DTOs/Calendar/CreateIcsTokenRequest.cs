namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Request to create a new ICS feed token.
/// </summary>
public class CreateIcsTokenRequest
{
    /// <summary>
    /// Optional label to identify the token (e.g., "Google Calendar", "Outlook").
    /// </summary>
    public string? Label { get; set; }
}
