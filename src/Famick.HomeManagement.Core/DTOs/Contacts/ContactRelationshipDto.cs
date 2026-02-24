using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Contact relationship with target contact info
/// </summary>
public class ContactRelationshipDto
{
    public Guid Id { get; set; }
    public Guid SourceContactId { get; set; }
    public Guid TargetContactId { get; set; }

    /// <summary>
    /// Relationship type
    /// </summary>
    public RelationshipType RelationshipType { get; set; }

    /// <summary>
    /// Custom label for the relationship (optional)
    /// </summary>
    public string? CustomLabel { get; set; }

    /// <summary>
    /// Target contact's display name
    /// </summary>
    public string TargetContactName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the target contact is linked to a system user
    /// </summary>
    public bool TargetIsUserLinked { get; set; }

    /// <summary>
    /// Display text for the relationship
    /// </summary>
    public string DisplayText => !string.IsNullOrWhiteSpace(CustomLabel)
        ? CustomLabel
        : RelationshipType.ToString();

    public DateTime CreatedAt { get; set; }
}
