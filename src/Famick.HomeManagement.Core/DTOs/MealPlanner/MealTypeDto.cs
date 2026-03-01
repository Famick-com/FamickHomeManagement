namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public string? Color { get; set; }
}
