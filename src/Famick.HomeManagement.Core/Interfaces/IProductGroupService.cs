using Famick.HomeManagement.Core.DTOs.ProductGroups;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing product groups (categories)
/// </summary>
public interface IProductGroupService
{
    /// <summary>
    /// Creates a new product group
    /// </summary>
    Task<ProductGroupDto> CreateAsync(
        CreateProductGroupRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a product group by ID
    /// </summary>
    Task<ProductGroupDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all product groups with optional filtering
    /// </summary>
    Task<List<ProductGroupDto>> ListAsync(
        ProductGroupFilterRequest? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing product group
    /// </summary>
    Task<ProductGroupDto> UpdateAsync(
        Guid id,
        UpdateProductGroupRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a product group (soft delete)
    /// </summary>
    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all products in a specific group
    /// </summary>
    Task<List<ProductSummaryDto>> GetProductsInGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);
}
