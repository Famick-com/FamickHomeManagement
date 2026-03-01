using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

[ApiController]
[Route("api/v1/meals")]
[Authorize]
public class MealsController : ApiControllerBase
{
    private readonly IMealService _service;
    private readonly IValidator<CreateMealRequest> _createValidator;
    private readonly IValidator<UpdateMealRequest> _updateValidator;

    public MealsController(
        IMealService service,
        IValidator<CreateMealRequest> createValidator,
        IValidator<UpdateMealRequest> updateValidator,
        ITenantProvider tenantProvider,
        ILogger<MealsController> logger)
        : base(tenantProvider, logger)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] MealFilterRequest? filter, CancellationToken ct)
    {
        var result = await _service.ListAsync(filter, ct);
        return ApiResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result == null ? NotFoundResponse() : ApiResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMealRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationErrorResponse(new Dictionary<string, string[]>(validation.ToDictionary()));

        var result = await _service.CreateAsync(request, ct);
        return ApiResponse(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMealRequest request, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationErrorResponse(new Dictionary<string, string[]>(validation.ToDictionary()));

        try
        {
            var result = await _service.UpdateAsync(id, request, ct);
            return ApiResponse(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse();
        }
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
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
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

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions(CancellationToken ct)
    {
        var result = await _service.GetSuggestionsAsync(ct);
        return ApiResponse(result);
    }

    [HttpGet("{id:guid}/allergen-check")]
    public async Task<IActionResult> CheckAllergens(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _service.CheckAllergensAsync(id, ct);
            return ApiResponse(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse();
        }
    }
}
