using Famick.HomeManagement.Core.DTOs.ProductGroups;
using Famick.HomeManagement.Core.DTOs.ShoppingLocations;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing shopping locations (stores)
/// </summary>
[ApiController]
[Route("api/v1/shoppinglocations")]
[Authorize]
public class ShoppingLocationsController : ApiControllerBase
{
    private readonly IShoppingLocationService _shoppingLocationService;
    private readonly IValidator<CreateShoppingLocationRequest> _createValidator;
    private readonly IValidator<UpdateShoppingLocationRequest> _updateValidator;

    public ShoppingLocationsController(
        IShoppingLocationService shoppingLocationService,
        IValidator<CreateShoppingLocationRequest> createValidator,
        IValidator<UpdateShoppingLocationRequest> updateValidator,
        ITenantProvider tenantProvider,
        ILogger<ShoppingLocationsController> logger)
        : base(tenantProvider, logger)
    {
        _shoppingLocationService = shoppingLocationService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Lists all shopping locations with optional filtering
    /// </summary>
    /// <param name="filter">Optional filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of shopping locations</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ShoppingLocationDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> List(
        [FromQuery] ShoppingLocationFilterRequest? filter,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing shopping locations for tenant {TenantId}", TenantId);

        var shoppingLocations = await _shoppingLocationService.ListAsync(filter, cancellationToken);
        return ApiResponse(shoppingLocations);
    }

    /// <summary>
    /// Gets a specific shopping location by ID
    /// </summary>
    /// <param name="id">Shopping location ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Shopping location details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ShoppingLocationDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting shopping location {ShoppingLocationId} for tenant {TenantId}", id, TenantId);

        var shoppingLocation = await _shoppingLocationService.GetByIdAsync(id, cancellationToken);

        if (shoppingLocation == null)
        {
            return NotFoundResponse($"Shopping location with ID {id} not found");
        }

        return ApiResponse(shoppingLocation);
    }

    /// <summary>
    /// Creates a new shopping location
    /// </summary>
    /// <param name="request">Shopping location creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created shopping location</returns>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ShoppingLocationDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create(
        [FromBody] CreateShoppingLocationRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Creating shopping location '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var shoppingLocation = await _shoppingLocationService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = shoppingLocation.Id },
            shoppingLocation
        );
    }

    /// <summary>
    /// Updates an existing shopping location
    /// </summary>
    /// <param name="id">Shopping location ID</param>
    /// <param name="request">Shopping location update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated shopping location</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ShoppingLocationDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateShoppingLocationRequest request,
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

        _logger.LogInformation("Updating shopping location {ShoppingLocationId} for tenant {TenantId}", id, TenantId);

        var shoppingLocation = await _shoppingLocationService.UpdateAsync(id, request, cancellationToken);
        return ApiResponse(shoppingLocation);
    }

    /// <summary>
    /// Deletes a shopping location (soft delete)
    /// </summary>
    /// <param name="id">Shopping location ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting shopping location {ShoppingLocationId} for tenant {TenantId}", id, TenantId);

        await _shoppingLocationService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Gets all products available at a specific shopping location
    /// </summary>
    /// <param name="id">Shopping location ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of products at the location</returns>
    [HttpGet("{id}/products")]
    [ProducesResponseType(typeof(List<ProductSummaryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetProducts(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting products at location {ShoppingLocationId} for tenant {TenantId}", id, TenantId);

        var products = await _shoppingLocationService.GetProductsAtLocationAsync(id, cancellationToken);
        return ApiResponse(products);
    }

    /// <summary>
    /// Gets the aisle order configuration for a store, including known aisles
    /// </summary>
    /// <param name="id">Shopping location ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aisle order configuration</returns>
    [HttpGet("{id}/aisle-order")]
    [ProducesResponseType(typeof(AisleOrderDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetAisleOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting aisle order for location {ShoppingLocationId} for tenant {TenantId}", id, TenantId);

        var aisleOrder = await _shoppingLocationService.GetAisleOrderAsync(id, cancellationToken);
        return ApiResponse(aisleOrder);
    }

    /// <summary>
    /// Updates the custom aisle order for a store
    /// </summary>
    /// <param name="id">Shopping location ID</param>
    /// <param name="request">Aisle order update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated aisle order configuration</returns>
    [HttpPut("{id}/aisle-order")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(AisleOrderDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateAisleOrder(
        Guid id,
        [FromBody] UpdateAisleOrderRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating aisle order for location {ShoppingLocationId} for tenant {TenantId}", id, TenantId);

        var aisleOrder = await _shoppingLocationService.UpdateAisleOrderAsync(id, request, cancellationToken);
        return ApiResponse(aisleOrder);
    }

    /// <summary>
    /// Clears the custom aisle order for a store (reverts to default ordering)
    /// </summary>
    /// <param name="id">Shopping location ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}/aisle-order")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ClearAisleOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Clearing aisle order for location {ShoppingLocationId} for tenant {TenantId}", id, TenantId);

        await _shoppingLocationService.ClearAisleOrderAsync(id, cancellationToken);
        return NoContent();
    }
}
