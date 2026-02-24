namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Contact tag definition
/// </summary>
public class ContactTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }

    /// <summary>
    /// Number of contacts with this tag
    /// </summary>
    public int ContactCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
