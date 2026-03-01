using Famick.HomeManagement.Core.DTOs.MealPlanner;

namespace Famick.HomeManagement.Core.Interfaces;

public interface IMealService
{
    Task<MealDto> CreateAsync(CreateMealRequest request, CancellationToken ct = default);
    Task<MealDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<MealSummaryDto>> ListAsync(MealFilterRequest? filter = null, CancellationToken ct = default);
    Task<MealDto> UpdateAsync(Guid id, UpdateMealRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<MealNutritionDto> GetNutritionAsync(Guid id, CancellationToken ct = default);
    Task<MealSuggestionDto> GetSuggestionsAsync(CancellationToken ct = default);
    Task<AllergenCheckResultDto> CheckAllergensAsync(Guid id, CancellationToken ct = default);
}
