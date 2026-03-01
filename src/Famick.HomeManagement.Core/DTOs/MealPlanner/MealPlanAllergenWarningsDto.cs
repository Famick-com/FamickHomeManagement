namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealPlanAllergenWarningsDto
{
    public Guid MealPlanId { get; set; }
    public bool HasWarnings { get; set; }
    public List<AllergenWarningDto> Warnings { get; set; } = new();
}
