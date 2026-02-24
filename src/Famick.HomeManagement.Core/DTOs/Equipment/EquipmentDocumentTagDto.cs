namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Equipment document tag data transfer object
/// </summary>
public class EquipmentDocumentTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public int DocumentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
