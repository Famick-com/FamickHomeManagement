using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

[ApiController]
[Route("api/v1/meal-types")]
[Authorize]
public class MealTypesController : ApiControllerBase
{
    private readonly IMealTypeService _service;
    private readonly IValidator<CreateMealTypeRequest> _createValidator;
    private readonly IValidator<UpdateMealTypeRequest> _updateValidator;

    public MealTypesController(
        IMealTypeService service,
        IValidator<CreateMealTypeRequest> createValidator,
        IValidator<UpdateMealTypeRequest> updateValidator,
        ITenantProvider tenantProvider,
        ILogger<MealTypesController> logger)
        : base(tenantProvider, logger)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _service.ListAsync(ct);
        return ApiResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMealTypeRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationErrorResponse(new Dictionary<string, string[]>(validation.ToDictionary()));

        try
        {
            var result = await _service.CreateAsync(request, ct);
            return ApiResponse(result);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMealTypeRequest request, CancellationToken ct)
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
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
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
}
