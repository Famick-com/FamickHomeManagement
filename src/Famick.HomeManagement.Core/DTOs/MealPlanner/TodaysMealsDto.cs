namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class TodaysMealsDto
{
    public DateOnly Date { get; set; }
    public List<TodaysMealGroupDto> MealGroups { get; set; } = new();
}

public class TodaysMealGroupDto
{
    public Guid MealTypeId { get; set; }
    public string MealTypeName { get; set; } = string.Empty;
    public string? MealTypeColor { get; set; }
    public List<MealPlanEntryDto> Entries { get; set; } = new();
}
