using Famick.HomeManagement.Core.DTOs.ProductGroups;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing product groups (categories)
/// </summary>
[ApiController]
[Route("api/v1/productgroups")]
[Authorize]
public class ProductGroupsController : ApiControllerBase
{
    private readonly IProductGroupService _productGroupService;
    private readonly IValidator<CreateProductGroupRequest> _createValidator;
    private readonly IValidator<UpdateProductGroupRequest> _updateValidator;

    public ProductGroupsController(
        IProductGroupService productGroupService,
        IValidator<CreateProductGroupRequest> createValidator,
        IValidator<UpdateProductGroupRequest> updateValidator,
        ITenantProvider tenantProvider,
        ILogger<ProductGroupsController> logger)
        : base(tenantProvider, logger)
    {
        _productGroupService = productGroupService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Lists all product groups with optional filtering
    /// </summary>
    /// <param name="filter">Optional filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of product groups</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProductGroupDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> List(
        [FromQuery] ProductGroupFilterRequest? filter,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing product groups for tenant {TenantId}", TenantId);

        var productGroups = await _productGroupService.ListAsync(filter, cancellationToken);
        return ApiResponse(productGroups);
    }

    /// <summary>
    /// Gets a specific product group by ID
    /// </summary>
    /// <param name="id">Product group ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product group details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductGroupDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting product group {ProductGroupId} for tenant {TenantId}", id, TenantId);

        var productGroup = await _productGroupService.GetByIdAsync(id, cancellationToken);

        if (productGroup == null)
        {
            return NotFoundResponse($"Product group with ID {id} not found");
        }

        return ApiResponse(productGroup);
    }

    /// <summary>
    /// Creates a new product group
    /// </summary>
    /// <param name="request">Product group creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created product group</returns>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductGroupDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductGroupRequest request,
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

        _logger.LogInformation("Creating product group '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var productGroup = await _productGroupService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = productGroup.Id },
            productGroup
        );
    }

    /// <summary>
    /// Updates an existing product group
    /// </summary>
    /// <param name="id">Product group ID</param>
    /// <param name="request">Product group update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated product group</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductGroupDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductGroupRequest request,
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

        _logger.LogInformation("Updating product group {ProductGroupId} for tenant {TenantId}", id, TenantId);

        var productGroup = await _productGroupService.UpdateAsync(id, request, cancellationToken);
        return ApiResponse(productGroup);
    }

    /// <summary>
    /// Deletes a product group (soft delete)
    /// </summary>
    /// <param name="id">Product group ID</param>
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
        _logger.LogInformation("Deleting product group {ProductGroupId} for tenant {TenantId}", id, TenantId);

        await _productGroupService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Gets all products in a specific product group
    /// </summary>
    /// <param name="id">Product group ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of products in the group</returns>
    [HttpGet("{id}/products")]
    [ProducesResponseType(typeof(List<ProductSummaryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetProducts(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting products in group {ProductGroupId} for tenant {TenantId}", id, TenantId);

        var products = await _productGroupService.GetProductsInGroupAsync(id, cancellationToken);
        return ApiResponse(products);
    }
}
