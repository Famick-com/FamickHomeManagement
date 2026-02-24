using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Contact phone number with tag information
/// </summary>
public class ContactPhoneNumberDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }

    /// <summary>
    /// Phone number as entered by user
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Normalized phone number for searching (digits only)
    /// </summary>
    public string? NormalizedNumber { get; set; }

    /// <summary>
    /// Phone tag (Mobile, Home, Work, etc.)
    /// </summary>
    public PhoneTag Tag { get; set; }

    /// <summary>
    /// Whether this is the primary phone number
    /// </summary>
    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }
}
