namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class CreateMealPlanEntryRequest
{
    public Guid? MealId { get; set; }
    public string? InlineNote { get; set; }
    public Guid MealTypeId { get; set; }
    public int DayOfWeek { get; set; }
    public int SortOrder { get; set; }
    public bool IsBatchSource { get; set; }
    public Guid? BatchSourceEntryId { get; set; }
}
