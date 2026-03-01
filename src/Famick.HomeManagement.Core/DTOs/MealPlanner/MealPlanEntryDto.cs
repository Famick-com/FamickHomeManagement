namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealPlanEntryDto
{
    public Guid Id { get; set; }
    public Guid? MealId { get; set; }
    public string? MealName { get; set; }
    public string? InlineNote { get; set; }
    public Guid MealTypeId { get; set; }
    public string MealTypeName { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public int SortOrder { get; set; }
    public bool IsBatchSource { get; set; }
    public Guid? BatchSourceEntryId { get; set; }
    public bool HasAllergenWarnings { get; set; }
}
