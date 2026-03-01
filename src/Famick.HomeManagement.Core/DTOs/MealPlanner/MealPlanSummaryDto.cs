namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealPlanSummaryDto
{
    public Guid Id { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public int EntryCount { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
