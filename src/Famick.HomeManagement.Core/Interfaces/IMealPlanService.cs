using Famick.HomeManagement.Core.DTOs.MealPlanner;

namespace Famick.HomeManagement.Core.Interfaces;

public interface IMealPlanService
{
    Task<MealPlanDto> GetOrCreateForWeekAsync(DateOnly weekStartDate, CancellationToken ct = default);
    Task<MealPlanDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<MealPlanSummaryDto>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<MealPlanEntryDto> AddEntryAsync(Guid planId, CreateMealPlanEntryRequest request, uint expectedVersion, Guid userId, CancellationToken ct = default);
    Task<MealPlanEntryDto> UpdateEntryAsync(Guid planId, Guid entryId, UpdateMealPlanEntryRequest request, uint expectedVersion, Guid userId, CancellationToken ct = default);
    Task DeleteEntryAsync(Guid planId, Guid entryId, uint expectedVersion, Guid userId, string? batchAction = null, CancellationToken ct = default);
    Task<ShoppingListPreviewDto> GenerateShoppingListAsync(Guid planId, GenerateShoppingListRequest request, CancellationToken ct = default);
    Task<MealPlanNutritionDto> GetNutritionAsync(Guid planId, CancellationToken ct = default);
    Task<TodaysMealsDto> GetTodaysMealsAsync(CancellationToken ct = default);
    Task<MealPlanAllergenWarningsDto> GetAllergenWarningsAsync(Guid planId, CancellationToken ct = default);

    // Ingredient-level batch cooking
    Task<BatchCookItemDto> AddBatchCookItemAsync(Guid planId, Guid entryId, CreateBatchCookItemRequest request, uint expectedVersion, Guid userId, CancellationToken ct = default);
    Task RemoveBatchCookItemAsync(Guid planId, Guid entryId, Guid batchCookItemId, uint expectedVersion, Guid userId, CancellationToken ct = default);
    Task<List<BatchCookItemDto>> GetBatchCookItemsAsync(Guid planId, Guid entryId, CancellationToken ct = default);
    Task<BatchCookItemUsageDto> LinkBatchCookItemAsync(Guid planId, Guid entryId, LinkBatchCookItemRequest request, uint expectedVersion, Guid userId, CancellationToken ct = default);
    Task UnlinkBatchCookItemAsync(Guid planId, Guid entryId, Guid usageId, uint expectedVersion, Guid userId, CancellationToken ct = default);
    Task<List<BatchCookSuggestionDto>> GetBatchCookSuggestionsAsync(Guid planId, Guid entryId, CancellationToken ct = default);
}
