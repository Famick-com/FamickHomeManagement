namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class CreateMealRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public List<CreateMealItemRequest> Items { get; set; } = new();
}
