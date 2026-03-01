using Famick.HomeManagement.Core.DTOs.MealPlanner;

namespace Famick.HomeManagement.Core.Interfaces;

public interface IDietaryProfileService
{
    Task<DietaryProfileDto> GetAsync(Guid contactId, CancellationToken ct = default);
    Task<DietaryProfileDto> UpdateAsync(Guid contactId, UpdateDietaryProfileRequest request, CancellationToken ct = default);
}
