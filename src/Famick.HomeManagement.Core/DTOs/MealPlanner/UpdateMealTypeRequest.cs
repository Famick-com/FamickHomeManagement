namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class UpdateMealTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Color { get; set; }
}
