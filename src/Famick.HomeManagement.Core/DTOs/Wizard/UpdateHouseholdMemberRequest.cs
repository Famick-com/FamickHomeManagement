namespace Famick.HomeManagement.Core.DTOs.Wizard;

/// <summary>
/// Request to update a household member's relationship
/// </summary>
public class UpdateHouseholdMemberRequest
{
    /// <summary>
    /// Relationship to the current user
    /// </summary>
    public string? RelationshipType { get; set; }
}
