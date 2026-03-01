using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

[ApiController]
[Route("api/v1/meal-planner")]
[Authorize]
public class MealPlannerController : ApiControllerBase
{
    private readonly IMealPlannerOnboardingService _onboardingService;

    public MealPlannerController(
        IMealPlannerOnboardingService onboardingService,
        ITenantProvider tenantProvider,
        ILogger<MealPlannerController> logger)
        : base(tenantProvider, logger)
    {
        _onboardingService = onboardingService;
    }

    [HttpGet("onboarding")]
    public async Task<IActionResult> GetOnboarding(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return UnauthorizedResponse();

        var result = await _onboardingService.GetOnboardingStateAsync(userId.Value, ct);
        return ApiResponse(result);
    }

    [HttpPost("onboarding")]
    public async Task<IActionResult> SaveOnboarding([FromBody] SaveOnboardingRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return UnauthorizedResponse();

        await _onboardingService.SaveOnboardingAsync(userId.Value, request, ct);
        return EmptyApiResponse();
    }

    [HttpPost("onboarding/reset")]
    public async Task<IActionResult> ResetOnboarding(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return UnauthorizedResponse();

        await _onboardingService.ResetOnboardingAsync(userId.Value, ct);
        return EmptyApiResponse();
    }

    [HttpGet("tips")]
    public async Task<IActionResult> GetTips(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return UnauthorizedResponse();

        var result = await _onboardingService.GetUndismissedTipsAsync(userId.Value, ct);
        return ApiResponse(result);
    }

    [HttpPost("tips/{tipKey}/dismiss")]
    public async Task<IActionResult> DismissTip(string tipKey, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return UnauthorizedResponse();

        await _onboardingService.DismissTipAsync(userId.Value, tipKey, ct);
        return EmptyApiResponse();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
