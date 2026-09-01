using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.FeatureFlags;
using FlagNames = Famick.HomeManagement.FeatureFlags.FeatureFlags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

[ApiController]
[Route("api/v1/product-onboarding")]
[Authorize]
public class ProductOnboardingController : ApiControllerBase
{
    private readonly IProductOnboardingService _service;
    private readonly IFeatureFlagService _featureFlags;

    public ProductOnboardingController(
        IProductOnboardingService service,
        IFeatureFlagService featureFlags,
        ITenantProvider tenantProvider,
        ILogger<ProductOnboardingController> logger)
        : base(tenantProvider, logger)
    {
        _service = service;
        _featureFlags = featureFlags;
    }

    [HttpGet]
    public async Task<IActionResult> GetState(CancellationToken ct)
    {
        var result = await _service.GetStateAsync(TenantId, ct);
        return ApiResponse(result);
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] ProductOnboardingCompleteRequest request, CancellationToken ct)
    {
        // The questionnaire's allergen and dietary answers are health information about the
        // household, and the service keeps the answers so the wizard can show them again.
        // Dropped here while that collection is switched off — the rest of the answers are
        // untouched, and a client that has not been updated simply has these ignored rather
        // than stored.
        if (!await _featureFlags.IsEnabledAsync(FlagNames.DietaryProfilesEnabled, ct))
        {
            request.Answers.Allergens.Clear();
            request.Answers.DietaryPreferences.Clear();
        }

        var result = await _service.CompleteAsync(TenantId, request, ct);
        return ApiResponse(result);
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await _service.ResetAsync(TenantId, ct);
        return EmptyApiResponse();
    }
}
