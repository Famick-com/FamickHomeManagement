using Famick.HomeManagement.Core.DTOs.MealPlanner;

namespace Famick.HomeManagement.Core.Interfaces;

public interface IMealPlannerOnboardingService
{
    Task<OnboardingStateDto> GetOnboardingStateAsync(Guid userId, CancellationToken ct = default);
    Task SaveOnboardingAsync(Guid userId, SaveOnboardingRequest request, CancellationToken ct = default);
    Task ResetOnboardingAsync(Guid userId, CancellationToken ct = default);
    Task<List<FeatureTipDto>> GetUndismissedTipsAsync(Guid userId, CancellationToken ct = default);
    Task DismissTipAsync(Guid userId, string tipKey, CancellationToken ct = default);
}
