namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public List<MealItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
