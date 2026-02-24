namespace Famick.HomeManagement.Core.DTOs.Wizard;

/// <summary>
/// Result of a duplicate contact check
/// </summary>
public class DuplicateContactResultDto
{
    /// <summary>
    /// Whether duplicates were found
    /// </summary>
    public bool HasDuplicates { get; set; }

    /// <summary>
    /// List of matching contacts
    /// </summary>
    public List<DuplicateContactMatchDto> Matches { get; set; } = new();
}

/// <summary>
/// A potential duplicate contact match
/// </summary>
public class DuplicateContactMatchDto
{
    public Guid ContactId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfileImageFileName { get; set; }

    /// <summary>
    /// Whether this contact is already a household member
    /// </summary>
    public bool IsHouseholdMember { get; set; }

    /// <summary>
    /// Match confidence (e.g., "Exact", "Similar")
    /// </summary>
    public string MatchType { get; set; } = "Exact";
}
