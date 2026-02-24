using Famick.HomeManagement.Core.DTOs.Home;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing home information, utilities, and setup
/// </summary>
[ApiController]
[Route("api/v1/home")]
[Authorize]
public class HomeController : ApiControllerBase
{
    private readonly IHomeService _homeService;
    private readonly IValidator<HomeSetupRequest> _setupValidator;
    private readonly IValidator<UpdateHomeRequest> _updateValidator;
    private readonly IValidator<CreateHomeUtilityRequest> _createUtilityValidator;
    private readonly IValidator<UpdateHomeUtilityRequest> _updateUtilityValidator;

    public HomeController(
        IHomeService homeService,
        IValidator<HomeSetupRequest> setupValidator,
        IValidator<UpdateHomeRequest> updateValidator,
        IValidator<CreateHomeUtilityRequest> createUtilityValidator,
        IValidator<UpdateHomeUtilityRequest> updateUtilityValidator,
        ITenantProvider tenantProvider,
        ILogger<HomeController> logger)
        : base(tenantProvider, logger)
    {
        _homeService = homeService;
        _setupValidator = setupValidator;
        _updateValidator = updateValidator;
        _createUtilityValidator = createUtilityValidator;
        _updateUtilityValidator = updateUtilityValidator;
    }

    #region Home

    /// <summary>
    /// Gets the home information for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HomeDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting home for tenant {TenantId}", TenantId);

        var home = await _homeService.GetHomeAsync(cancellationToken);

        if (home == null)
        {
            return NotFoundResponse("Home not found. Please complete the setup wizard.");
        }

        return ApiResponse(home);
    }

    /// <summary>
    /// Checks if the home setup has been completed
    /// </summary>
    [HttpGet("setup-status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSetupStatus(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking home setup status for tenant {TenantId}", TenantId);

        var isComplete = await _homeService.IsHomeSetupCompleteAsync(cancellationToken);

        return ApiResponse(new { isSetupComplete = isComplete });
    }

    /// <summary>
    /// Completes the initial home setup wizard
    /// </summary>
    [HttpPost("setup")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(HomeDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Setup(
        [FromBody] HomeSetupRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _setupValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Setting up home for tenant {TenantId}", TenantId);

        var home = await _homeService.SetupHomeAsync(request, cancellationToken);

        return CreatedAtAction(nameof(Get), home);
    }

    /// <summary>
    /// Updates the home information
    /// </summary>
    [HttpPut]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(HomeDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateHomeRequest request,
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

        _logger.LogInformation("Updating home for tenant {TenantId}", TenantId);

        var home = await _homeService.UpdateHomeAsync(request, cancellationToken);

        return ApiResponse(home);
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Adds a new utility to the home
    /// </summary>
    [HttpPost("utilities")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(HomeUtilityDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> AddUtility(
        [FromBody] CreateHomeUtilityRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createUtilityValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Adding utility {UtilityType} for tenant {TenantId}", request.UtilityType, TenantId);

        var utility = await _homeService.AddUtilityAsync(request, cancellationToken);

        return CreatedAtAction(nameof(Get), utility);
    }

    /// <summary>
    /// Updates an existing utility
    /// </summary>
    [HttpPut("utilities/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(HomeUtilityDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateUtility(
        Guid id,
        [FromBody] UpdateHomeUtilityRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _updateUtilityValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Updating utility {UtilityId} for tenant {TenantId}", id, TenantId);

        var utility = await _homeService.UpdateUtilityAsync(id, request, cancellationToken);

        return ApiResponse(utility);
    }

    /// <summary>
    /// Deletes a utility from the home
    /// </summary>
    [HttpDelete("utilities/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteUtility(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting utility {UtilityId} for tenant {TenantId}", id, TenantId);

        await _homeService.DeleteUtilityAsync(id, cancellationToken);

        return NoContent();
    }

    #endregion

    #region Property Links

    /// <summary>
    /// Gets all property links for the home
    /// </summary>
    [HttpGet("property-links")]
    [ProducesResponseType(typeof(List<PropertyLinkDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetPropertyLinks(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting property links for tenant {TenantId}", TenantId);

        var links = await _homeService.GetPropertyLinksAsync(cancellationToken);

        return ApiResponse(links);
    }

    /// <summary>
    /// Adds a property link to the home
    /// </summary>
    [HttpPost("property-links")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(PropertyLinkDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddPropertyLink(
        [FromBody] CreatePropertyLinkRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding property link {Label} for tenant {TenantId}", request.Label, TenantId);

        var link = await _homeService.AddPropertyLinkAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetPropertyLinks), link);
    }

    /// <summary>
    /// Updates a property link
    /// </summary>
    [HttpPut("property-links/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(PropertyLinkDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdatePropertyLink(
        Guid id,
        [FromBody] UpdatePropertyLinkRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating property link {LinkId} for tenant {TenantId}", id, TenantId);

        var link = await _homeService.UpdatePropertyLinkAsync(id, request, cancellationToken);

        return ApiResponse(link);
    }

    /// <summary>
    /// Deletes a property link
    /// </summary>
    [HttpDelete("property-links/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeletePropertyLink(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting property link {LinkId} for tenant {TenantId}", id, TenantId);

        await _homeService.DeletePropertyLinkAsync(id, cancellationToken);

        return NoContent();
    }

    #endregion
}
