using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Filter parameters for searching contacts
/// </summary>
public class ContactFilterRequest
{
    /// <summary>
    /// Search term for name, email, or phone
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by visibility level
    /// </summary>
    public ContactVisibilityLevel? Visibility { get; set; }

    /// <summary>
    /// Filter by tag IDs (any match)
    /// </summary>
    public List<Guid>? TagIds { get; set; }

    /// <summary>
    /// Filter by active status
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Filter to show only user-linked contacts
    /// </summary>
    public bool? IsUserLinked { get; set; }

    /// <summary>
    /// Filter by contact group type (Household or Business)
    /// </summary>
    public ContactType? ContactType { get; set; }

    /// <summary>
    /// Filter members by parent group ID
    /// </summary>
    public Guid? ParentContactId { get; set; }

    /// <summary>
    /// Filter groups vs members (true = groups only, false = members only)
    /// </summary>
    public bool? IsGroup { get; set; }

    /// <summary>
    /// Filter by relationship type to a specific contact
    /// </summary>
    public Guid? RelatedToContactId { get; set; }
    public RelationshipType? RelationshipType { get; set; }

    /// <summary>
    /// Sort field
    /// </summary>
    public string SortBy { get; set; } = "LastName";

    /// <summary>
    /// Sort direction
    /// </summary>
    public bool SortDescending { get; set; } = false;

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size
    /// </summary>
    public int PageSize { get; set; } = 25;
}
