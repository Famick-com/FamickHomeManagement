using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to add a phone number to a contact
/// </summary>
public class AddPhoneRequest
{
    /// <summary>
    /// Phone number
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Phone tag (Mobile, Home, Work, etc.)
    /// </summary>
    public PhoneTag Tag { get; set; } = PhoneTag.Mobile;

    /// <summary>
    /// Whether to set this as the primary phone number
    /// </summary>
    public bool IsPrimary { get; set; } = false;
}
