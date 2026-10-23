namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// What removing a member can offer on this server.
/// </summary>
public class MemberRemovalOptionsDto
{
    /// <summary>
    /// Whether the member can be emailed a copy of their own data first. False where mail is not
    /// set up, or where the link would not work from an email client — the copy goes only to the
    /// member, so without email there is no way to send it.
    /// </summary>
    public bool CanEmailDataExport { get; set; }
}
