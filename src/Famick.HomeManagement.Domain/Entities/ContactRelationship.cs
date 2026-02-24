using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a relationship between two contacts.
/// Relationships are directional: SourceContact is X to TargetContact.
/// Example: If John (source) is the Father of Jane (target),
/// then Jane would have an inverse relationship where she is Daughter to John.
/// </summary>
public class ContactRelationship : BaseTenantEntity
{
    /// <summary>
    /// The contact who has this relationship
    /// </summary>
    public Guid SourceContactId { get; set; }

    /// <summary>
    /// The contact this relationship is with
    /// </summary>
    public Guid TargetContactId { get; set; }

    /// <summary>
    /// Type of relationship (e.g., Mother, Father, Friend)
    /// </summary>
    public RelationshipType RelationshipType { get; set; }

    /// <summary>
    /// Custom label when RelationshipType is Other
    /// </summary>
    public string? CustomLabel { get; set; }

    /// <summary>
    /// Optional notes about this relationship
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Contact SourceContact { get; set; } = null!;
    public virtual Contact TargetContact { get; set; } = null!;
}
