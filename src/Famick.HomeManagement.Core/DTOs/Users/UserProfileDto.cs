using Famick.HomeManagement.Core.DTOs.Contacts;

namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Data transfer object for user's own profile
/// </summary>
public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PreferredLanguage { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Full name (convenience property)
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Whether the user has a password set (for external auth users, this may be false)
    /// </summary>
    public bool HasPassword { get; set; }

    /// <summary>
    /// Linked contact ID (if any)
    /// </summary>
    public Guid? ContactId { get; set; }

    /// <summary>
    /// Linked contact details for embedded editing
    /// </summary>
    public ContactDto? Contact { get; set; }
}
