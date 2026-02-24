using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to add an email address to a contact
/// </summary>
public class AddEmailRequest
{
    /// <summary>
    /// Email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Email tag (Personal, Work, School, etc.)
    /// </summary>
    public EmailTag Tag { get; set; } = EmailTag.Personal;

    /// <summary>
    /// Whether to set this as the primary email address
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// Optional custom label
    /// </summary>
    public string? Label { get; set; }
}
