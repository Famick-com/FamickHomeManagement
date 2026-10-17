using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Famick.HomeManagement.Web.Shared.Authorization;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

[ApiController]
[Route("api/v1/meal-planner")]
[Authorize]
public class MealPlannerController : ApiControllerBase
{
    private readonly IMealPlannerOnboardingService _onboardingService;
    private readonly IMealTypeService _mealTypeService;
    private readonly IAuthorizationService _authorizationService;

    public MealPlannerController(
        IMealPlannerOnboardingService onboardingService,
        IMealTypeService mealTypeService,
        IAuthorizationService authorizationService,
        ITenantProvider tenantProvider,
        ILogger<MealPlannerController> logger)
        : base(tenantProvider, logger)
    {
        _onboardingService = onboardingService;
        _mealTypeService = mealTypeService;
        _authorizationService = authorizationService;
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
    [NoRolePolicy("Per-user onboarding state; household meal types are only created for Editors.")]
    public async Task<IActionResult> SaveOnboarding([FromBody] SaveOnboardingRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return UnauthorizedResponse();

        await _onboardingService.SaveOnboardingAsync(userId.Value, request, ct);

        // Create user-selected meal types, or fall back to seeding defaults. Onboarding is
        // per-user and every member sees it, but choosing the household's meal types is an
        // Editor's call — a Viewer's choices are dropped and the defaults apply.
        var canEdit = (await _authorizationService.AuthorizeAsync(User, "RequireEditor")).Succeeded;
        if (canEdit && request.MealTypes?.Count > 0)
            await _mealTypeService.CreateFromOnboardingAsync(TenantId, request.MealTypes, ct);
        else
            await _mealTypeService.SeedDefaultsForTenantAsync(TenantId, ct);

        return EmptyApiResponse();
    }

    [HttpPost("onboarding/reset")]
    [NoRolePolicy("Per-user onboarding state.")]
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
    [NoRolePolicy("Per-user onboarding state.")]
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
