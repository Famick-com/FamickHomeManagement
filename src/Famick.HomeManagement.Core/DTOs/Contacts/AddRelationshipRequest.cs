using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to add a relationship between contacts
/// </summary>
public class AddRelationshipRequest
{
    /// <summary>
    /// Target contact ID
    /// </summary>
    public Guid TargetContactId { get; set; }

    /// <summary>
    /// Relationship type from source to target
    /// </summary>
    public RelationshipType RelationshipType { get; set; }

    /// <summary>
    /// Custom label for the relationship (optional)
    /// </summary>
    public string? CustomLabel { get; set; }

    /// <summary>
    /// Whether to create the inverse relationship automatically
    /// </summary>
    public bool CreateInverse { get; set; } = true;
}
