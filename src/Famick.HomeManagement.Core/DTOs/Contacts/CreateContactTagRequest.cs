namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to create a new contact tag
/// </summary>
public class CreateContactTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
}
