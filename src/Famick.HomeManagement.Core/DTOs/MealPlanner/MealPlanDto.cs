namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealPlanDto
{
    public Guid Id { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByUserName { get; set; }
    public uint Version { get; set; }
    public List<MealPlanEntryDto> Entries { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
