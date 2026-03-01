using Famick.HomeManagement.Core.DTOs.MealPlanner;

namespace Famick.HomeManagement.Core.Interfaces;

public interface IAllergenWarningService
{
    Task<AllergenCheckResultDto> CheckMealAsync(Guid mealId, CancellationToken ct = default);
    Task<MealPlanAllergenWarningsDto> CheckMealPlanAsync(Guid mealPlanId, CancellationToken ct = default);
}
