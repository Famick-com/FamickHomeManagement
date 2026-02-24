namespace Famick.HomeManagement.Core.DTOs.Wizard;

/// <summary>
/// Request to add a household member
/// </summary>
public class AddHouseholdMemberRequest
{
    /// <summary>
    /// First name of the member
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Last name of the member (optional)
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Relationship to the current user (e.g., "Spouse", "Child", "Parent")
    /// </summary>
    public string? RelationshipType { get; set; }

    /// <summary>
    /// If linking to an existing contact, provide the ID here
    /// </summary>
    public Guid? ExistingContactId { get; set; }
}
