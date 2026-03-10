using Famick.HomeManagement.Core.DTOs.ProductOnboarding;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing product onboarding at the tenant (household) level.
/// </summary>
public interface IProductOnboardingService
{
    Task<ProductOnboardingStateDto> GetStateAsync(Guid tenantId, CancellationToken ct = default);
    Task<ProductOnboardingCompleteResponse> CompleteAsync(Guid tenantId, ProductOnboardingCompleteRequest request, CancellationToken ct = default);
    Task ResetAsync(Guid tenantId, CancellationToken ct = default);
}
