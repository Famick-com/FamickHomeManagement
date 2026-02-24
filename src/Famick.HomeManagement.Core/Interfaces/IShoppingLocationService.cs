using Famick.HomeManagement.Core.DTOs.ProductGroups;
using Famick.HomeManagement.Core.DTOs.ShoppingLocations;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing shopping locations (stores)
/// </summary>
public interface IShoppingLocationService
{
    /// <summary>
    /// Creates a new shopping location
    /// </summary>
    Task<ShoppingLocationDto> CreateAsync(
        CreateShoppingLocationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a shopping location by ID
    /// </summary>
    Task<ShoppingLocationDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all shopping locations with optional filtering
    /// </summary>
    Task<List<ShoppingLocationDto>> ListAsync(
        ShoppingLocationFilterRequest? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing shopping location
    /// </summary>
    Task<ShoppingLocationDto> UpdateAsync(
        Guid id,
        UpdateShoppingLocationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a shopping location (soft delete)
    /// </summary>
    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all products at a specific shopping location
    /// </summary>
    Task<List<ProductSummaryDto>> GetProductsAtLocationAsync(
        Guid locationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the aisle order configuration for a store, including all known aisles
    /// </summary>
    Task<AisleOrderDto> GetAisleOrderAsync(
        Guid locationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the custom aisle order for a store
    /// </summary>
    Task<AisleOrderDto> UpdateAisleOrderAsync(
        Guid locationId,
        UpdateAisleOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the custom aisle order for a store (reverts to default ordering)
    /// </summary>
    Task ClearAisleOrderAsync(
        Guid locationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all known aisles for a store from ProductStoreMetadata
    /// </summary>
    Task<List<string>> GetKnownAislesAsync(
        Guid locationId,
        CancellationToken cancellationToken = default);
}
