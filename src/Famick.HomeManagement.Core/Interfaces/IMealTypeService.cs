using Famick.HomeManagement.Core.DTOs.MealPlanner;

namespace Famick.HomeManagement.Core.Interfaces;

public interface IMealTypeService
{
    Task<List<MealTypeDto>> ListAsync(CancellationToken ct = default);
    Task<MealTypeDto> CreateAsync(CreateMealTypeRequest request, CancellationToken ct = default);
    Task<MealTypeDto> UpdateAsync(Guid id, UpdateMealTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task SeedDefaultsForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task CreateFromOnboardingAsync(Guid tenantId, List<OnboardingMealTypeSelection> selections, CancellationToken ct = default);
}
