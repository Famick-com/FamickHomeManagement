namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to create a new vCard feed token.
/// </summary>
public class CreateVcfTokenRequest
{
    /// <summary>
    /// Optional label to identify the token (e.g., "macOS Contacts", "Outlook").
    /// </summary>
    public string? Label { get; set; }
}
