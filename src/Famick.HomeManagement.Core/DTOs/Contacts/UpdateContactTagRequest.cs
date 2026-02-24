namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to update an existing contact tag
/// </summary>
public class UpdateContactTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
}
