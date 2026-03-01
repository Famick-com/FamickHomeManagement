namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealSuggestionDto
{
    public List<MealSummaryDto> ReadyToCook { get; set; } = new();
    public List<MealSummaryDto> AlmostReady { get; set; } = new();
    public List<MealSummaryDto> Favorites { get; set; } = new();
    public List<MealSummaryDto> Recent { get; set; } = new();
}
