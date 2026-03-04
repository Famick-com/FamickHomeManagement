using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

[ApiController]
[Route("api/v1/meal-plans")]
[Authorize]
public class MealPlansController : ApiControllerBase
{
    private readonly IMealPlanService _service;
    private readonly IValidator<CreateMealPlanEntryRequest> _createEntryValidator;
    private readonly IValidator<UpdateMealPlanEntryRequest> _updateEntryValidator;
    private readonly IValidator<GenerateShoppingListRequest> _generateShoppingListValidator;

    public MealPlansController(
        IMealPlanService service,
        IValidator<CreateMealPlanEntryRequest> createEntryValidator,
        IValidator<UpdateMealPlanEntryRequest> updateEntryValidator,
        IValidator<GenerateShoppingListRequest> generateShoppingListValidator,
        ITenantProvider tenantProvider,
        ILogger<MealPlansController> logger)
        : base(tenantProvider, logger)
    {
        _service = service;
        _createEntryValidator = createEntryValidator;
        _updateEntryValidator = updateEntryValidator;
        _generateShoppingListValidator = generateShoppingListValidator;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _service.ListAsync(ct);
        return ApiResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result == null ? NotFoundResponse() : ApiResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> GetOrCreateForWeek([FromQuery] DateOnly weekStartDate, CancellationToken ct)
    {
        var result = await _service.GetOrCreateForWeekAsync(weekStartDate, ct);
        return ApiResponse(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, ct);
            return EmptyApiResponse();
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse();
        }
    }

    [HttpPost("{id:guid}/entries")]
    public async Task<IActionResult> AddEntry(
        Guid id,
        [FromBody] CreateMealPlanEntryRequest request,
        [FromQuery] uint version,
        CancellationToken ct)
    {
        var validation = await _createEntryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationErrorResponse(new Dictionary<string, string[]>(validation.ToDictionary()));

        var userId = GetCurrentUserId();
        if (userId == null)
            return UnauthorizedResponse();

        try
        {
            var result = await _service.AddEntryAsync(id, request, version, userId.Value, ct);
            return ApiResponse(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse();
        }
        catch (MealPlanConcurrencyException ex)
        {
            return StatusCode(409, new
            {
                error_message = ex.Message,
                updatedBy = ex.UpdatedByUserId
            });
        }
    }

    [HttpPut("{id:guid}/entries/{entryId:guid}")]
    public async Task<IActionResult> UpdateEntry(
        Guid id, Guid entryId,
        [FromBody] UpdateMealPlanEntryRequest request,
        [FromQuery] uint version,
        CancellationToken ct)
    {
        var validation = await _updateEntryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationErrorResponse(new Dictionary<string, string[]>(validation.ToDictionary()));

        var userId = GetCurrentUserId();
        if (userId == null)
            return UnauthorizedResponse();

        try
        {
            var result = await _service.UpdateEntryAsync(id, entryId, request, version, userId.Value, ct);
            return ApiResponse(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse();
        }
        catch (MealPlanConcurrencyException ex)
        {
            return StatusCode(409, new
            {
                error_message = ex.Message,
                updatedBy = ex.UpdatedByUserId
            });
        }
    }

    [HttpDelete("{id:guid}/entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(
        Guid id, Guid entryId,
        [FromQuery] uint version,
        [FromQuery] string? batchAction = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return UnauthorizedResponse();

        try
        {
            await _service.DeleteEntryAsync(id, entryId, version, userId.Value, batchAction, ct);
            return EmptyApiResponse();
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse();
        }
        catch (MealPlanConcurrencyException ex)
        {
            return StatusCode(409, new
            {
                error_message = ex.Message,
                updatedBy = ex.UpdatedByUserId
            });
        }
        catch (BatchSourceHasDependentsException ex)
        {
            return StatusCode(409, new
            {
                error_type = "batch_source_has_dependents",
                dependent_count = ex.DependentCount
            });
        }
    }

    [HttpPost("{id:guid}/generate-shopping-list")]
    public async Task<IActionResult> GenerateShoppingList(
        Guid id,
        [FromBody] GenerateShoppingListRequest request,
        CancellationToken ct)
    {
        var validation = await _generateShoppingListValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationErrorResponse(new Dictionary<string, string[]>(validation.ToDictionary()));

        try
        {
            var result = await _service.GenerateShoppingListAsync(id, request, ct);
            return ApiResponse(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse();
        }
    }

    [HttpGet("{id:guid}/nutrition")]
    public async Task<IActionResult> GetNutrition(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _service.GetNutritionAsync(id, ct);
            return ApiResponse(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse();
        }
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetTodaysMeals(CancellationToken ct)
    {
        var result = await _service.GetTodaysMealsAsync(ct);
        return ApiResponse(result);
    }

    [HttpGet("{id:guid}/allergen-warnings")]
    public async Task<IActionResult> GetAllergenWarnings(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _service.GetAllergenWarningsAsync(id, ct);
            return ApiResponse(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse();
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
