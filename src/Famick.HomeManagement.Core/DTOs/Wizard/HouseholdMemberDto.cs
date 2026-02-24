namespace Famick.HomeManagement.Core.DTOs.Wizard;

/// <summary>
/// Represents a household member with their relationship to the current user
/// </summary>
public class HouseholdMemberDto
{
    public Guid ContactId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfileImageFileName { get; set; }

    /// <summary>
    /// The relationship type (e.g., "Spouse", "Child", "Parent", "Self")
    /// </summary>
    public string? RelationshipType { get; set; }

    /// <summary>
    /// Whether this is the current user's contact
    /// </summary>
    public bool IsCurrentUser { get; set; }

    /// <summary>
    /// Whether this contact is linked to a user account
    /// </summary>
    public bool HasUserAccount { get; set; }

    /// <summary>
    /// The linked user's email (if applicable)
    /// </summary>
    public string? Email { get; set; }
}
