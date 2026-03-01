namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
