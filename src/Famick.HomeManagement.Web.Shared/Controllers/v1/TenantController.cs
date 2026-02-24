using Famick.HomeManagement.Core.DTOs.Tenant;
using Famick.HomeManagement.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing tenant (household) information
/// </summary>
[ApiController]
[Route("api/v1/tenant")]
[Authorize]
public class TenantController : ApiControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly IValidator<UpdateTenantRequest> _updateValidator;

    public TenantController(
        ITenantService tenantService,
        IValidator<UpdateTenantRequest> updateValidator,
        ITenantProvider tenantProvider,
        ILogger<TenantController> logger)
        : base(tenantProvider, logger)
    {
        _tenantService = tenantService;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Gets the current tenant's information including address
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TenantDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting tenant for ID {TenantId}", TenantId);

        var tenant = await _tenantService.GetCurrentTenantAsync(cancellationToken);

        if (tenant == null)
        {
            return NotFoundResponse("Tenant not found");
        }

        return ApiResponse(tenant);
    }

    /// <summary>
    /// Updates the current tenant's name and address
    /// </summary>
    [HttpPut]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(TenantDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Updating tenant {TenantId} with name: {Name}", TenantId, request.Name);

        var tenant = await _tenantService.UpdateCurrentTenantAsync(request, cancellationToken);
        return ApiResponse(tenant);
    }
}
