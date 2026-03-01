namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealPlanNutritionDto
{
    public Guid MealPlanId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public decimal WeeklyCalories { get; set; }
    public decimal WeeklyProteinGrams { get; set; }
    public decimal WeeklyCarbsGrams { get; set; }
    public decimal WeeklyFatGrams { get; set; }
    public List<DailyNutritionDto> DailyBreakdown { get; set; } = new();
}

public class DailyNutritionDto
{
    public int DayOfWeek { get; set; }
    public decimal Calories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatGrams { get; set; }
}
