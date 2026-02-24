using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Contact email address with tag information
/// </summary>
public class ContactEmailAddressDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }

    /// <summary>
    /// Email address as entered by user
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Normalized email for searching (lowercase, trimmed)
    /// </summary>
    public string? NormalizedEmail { get; set; }

    /// <summary>
    /// Email tag (Personal, Work, School, etc.)
    /// </summary>
    public EmailTag Tag { get; set; }

    /// <summary>
    /// Whether this is the primary email address
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Optional custom label
    /// </summary>
    public string? Label { get; set; }

    public DateTime CreatedAt { get; set; }
}
