using Famick.HomeManagement.Core.DTOs.ProductGroups;
using Famick.HomeManagement.Core.DTOs.ShoppingLocations;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Plugin.Abstractions.Authentication;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ShoppingLocationService : IShoppingLocationService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<ShoppingLocationService> _logger;

    private readonly IPluginLoader _pluginLoader;

    public ShoppingLocationService(
        HomeManagementDbContext context,
        IPluginLoader pluginLoader,
        ILogger<ShoppingLocationService> logger)
    {
        _context = context;
        _logger = logger;
        _pluginLoader = pluginLoader;
    }

    public async Task<ShoppingLocationDto> CreateAsync(
        CreateShoppingLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating shopping location: {Name}", request.Name);

        // Check for duplicate name
        var exists = await _context.ShoppingLocations
            .AnyAsync(sl => sl.Name == request.Name, cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException(nameof(ShoppingLocation), "Name", request.Name);
        }

        var shoppingLocation = ShoppingLocationMapper.FromCreateRequest(request);
        shoppingLocation.Id = Guid.NewGuid();

        _context.ShoppingLocations.Add(shoppingLocation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created shopping location: {Id} - {Name}", shoppingLocation.Id, shoppingLocation.Name);

        var dto = ShoppingLocationMapper.ToDto(shoppingLocation);
        await PopulateIsConnectedAsync(new[] { dto }, cancellationToken);
        return dto;
    }

    public async Task<ShoppingLocationDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var shoppingLocation = await _context.ShoppingLocations
            .Include(sl => sl.Products)
            .FirstOrDefaultAsync(sl => sl.Id == id, cancellationToken);

        if (shoppingLocation == null)
            return null;

        var dto = ShoppingLocationMapper.ToDto(shoppingLocation);
        await PopulateIsConnectedAsync(new[] { dto }, cancellationToken);
        return dto;
    }

    public async Task<List<ShoppingLocationDto>> ListAsync(
        ShoppingLocationFilterRequest? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ShoppingLocations
            .Include(sl => sl.Products)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(filter?.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(sl =>
                sl.Name.ToLower().Contains(searchTerm) ||
                (sl.Description != null && sl.Description.ToLower().Contains(searchTerm)));
        }

        // Apply sorting
        query = (filter?.SortBy?.ToLower()) switch
        {
            "name" => filter.Descending
                ? query.OrderByDescending(sl => sl.Name)
                : query.OrderBy(sl => sl.Name),
            "createdat" => filter.Descending
                ? query.OrderByDescending(sl => sl.CreatedAt)
                : query.OrderBy(sl => sl.CreatedAt),
            "productcount" => filter.Descending
                ? query.OrderByDescending(sl => sl.Products!.Count)
                : query.OrderBy(sl => sl.Products!.Count),
            _ => query.OrderBy(sl => sl.Name) // Default sort by name
        };

        var shoppingLocations = await query.ToListAsync(cancellationToken);

        var dtos = shoppingLocations.Select(ShoppingLocationMapper.ToDto).ToList();
        await PopulateIsConnectedAsync(dtos, cancellationToken);
        return dtos;
    }

    public async Task<ShoppingLocationDto> UpdateAsync(
        Guid id,
        UpdateShoppingLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating shopping location: {Id}", id);

        var shoppingLocation = await _context.ShoppingLocations.FindAsync(new object[] { id }, cancellationToken);
        if (shoppingLocation == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingLocation), id);
        }

        // Check for duplicate name (excluding current entity)
        var exists = await _context.ShoppingLocations
            .AnyAsync(sl => sl.Name == request.Name && sl.Id != id, cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException(nameof(ShoppingLocation), "Name", request.Name);
        }

        ShoppingLocationMapper.Update(request, shoppingLocation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated shopping location: {Id} - {Name}", id, request.Name);

        // Reload with products for DTO mapping
        shoppingLocation = await _context.ShoppingLocations
            .Include(sl => sl.Products)
            .FirstAsync(sl => sl.Id == id, cancellationToken);

        var dto = ShoppingLocationMapper.ToDto(shoppingLocation);
        await PopulateIsConnectedAsync(new[] { dto }, cancellationToken);
        return dto;
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting shopping location: {Id}", id);

        var shoppingLocation = await _context.ShoppingLocations.FindAsync(new object[] { id }, cancellationToken);
        if (shoppingLocation == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingLocation), id);
        }

        _context.ShoppingLocations.Remove(shoppingLocation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted shopping location: {Id}", id);
    }

    public async Task<List<ProductSummaryDto>> GetProductsAtLocationAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        var shoppingLocation = await _context.ShoppingLocations.FindAsync(new object[] { locationId }, cancellationToken);
        if (shoppingLocation == null)
        {
            throw new EntityNotFoundException(nameof(ShoppingLocation), locationId);
        }

        var products = await _context.Products
            .Include(p => p.ProductGroup)
            .Include(p => p.ShoppingLocation)
            .Where(p => p.ShoppingLocationId == locationId)
            .ToListAsync(cancellationToken);

        return products.Select(ProductGroupMapper.ToProductSummaryDto).ToList();
    }

    public async Task<List<string>> GetKnownAislesAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetKnownAislesAsync called for location: {LocationId}", locationId);

        // Query distinct non-null aisles from ProductStoreMetadata for this location
        var metadataAisles = await _context.ProductStoreMetadata
            .Where(psm => psm.ShoppingLocationId == locationId && psm.Aisle != null)
            .Select(psm => psm.Aisle!)
            .Distinct()
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} aisles from ProductStoreMetadata: {Aisles}",
            metadataAisles.Count, string.Join(", ", metadataAisles));

        // Also include aisles from shopping list items at this location
        // First get all shopping list IDs for this location
        var listIdsAtLocation = await _context.ShoppingLists
            .Where(sl => sl.ShoppingLocationId == locationId)
            .Select(sl => sl.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} shopping lists at location {LocationId}: {ListIds}",
            listIdsAtLocation.Count, locationId, string.Join(", ", listIdsAtLocation));

        var shoppingListAisles = await _context.ShoppingListItems
            .Where(sli => listIdsAtLocation.Contains(sli.ShoppingListId) && sli.Aisle != null && sli.Aisle != "")
            .Select(sli => sli.Aisle!)
            .Distinct()
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} aisles from ShoppingListItems: {Aisles}",
            shoppingListAisles.Count, string.Join(", ", shoppingListAisles));

        // Combine and deduplicate
        var allAisles = metadataAisles.Union(shoppingListAisles).Distinct().ToList();

        _logger.LogInformation("Combined {Count} unique aisles: {Aisles}",
            allAisles.Count, string.Join(", ", allAisles));

        // Sort using natural ordering (numeric first, then alphabetical)
        return SortAislesNaturally(allAisles);
    }

    public async Task<AisleOrderDto> GetAisleOrderAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        var location = await _context.ShoppingLocations
            .FindAsync(new object[] { locationId }, cancellationToken);

        if (location == null)
            throw new EntityNotFoundException(nameof(ShoppingLocation), locationId);

        var knownAisles = await GetKnownAislesAsync(locationId, cancellationToken);

        return new AisleOrderDto
        {
            OrderedAisles = location.AisleOrder ?? new List<string>(),
            KnownAisles = knownAisles
        };
    }

    public async Task<AisleOrderDto> UpdateAisleOrderAsync(
        Guid locationId,
        UpdateAisleOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating aisle order for shopping location: {LocationId}", locationId);

        var location = await _context.ShoppingLocations
            .FindAsync(new object[] { locationId }, cancellationToken);

        if (location == null)
            throw new EntityNotFoundException(nameof(ShoppingLocation), locationId);

        // Save the user's custom order exactly as provided
        location.AisleOrder = request.OrderedAisles != null && request.OrderedAisles.Count > 0
            ? request.OrderedAisles
            : null;

        // Explicitly mark JSONB property as modified - EF Core doesn't always detect JSONB changes
        _context.Entry(location).Property(x => x.AisleOrder).IsModified = true;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated aisle order for shopping location: {LocationId}", locationId);

        return await GetAisleOrderAsync(locationId, cancellationToken);
    }

    public async Task ClearAisleOrderAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing aisle order for shopping location: {LocationId}", locationId);

        var location = await _context.ShoppingLocations
            .FindAsync(new object[] { locationId }, cancellationToken);

        if (location == null)
            throw new EntityNotFoundException(nameof(ShoppingLocation), locationId);

        location.AisleOrder = null;

        // Explicitly mark JSONB property as modified - EF Core doesn't always detect JSONB changes
        _context.Entry(location).Property(x => x.AisleOrder).IsModified = true;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleared aisle order for shopping location: {LocationId}", locationId);
    }

    /// <summary>
    /// Sorts aisles naturally: numeric aisles first (by value), then named aisles alphabetically.
    /// </summary>
    private static List<string> SortAislesNaturally(List<string> aisles)
    {
        var numeric = aisles
            .Where(a => int.TryParse(a, out _))
            .OrderBy(a => int.Parse(a))
            .ToList();

        var named = aisles
            .Where(a => !int.TryParse(a, out _))
            .OrderBy(a => a)
            .ToList();

        return numeric.Concat(named).ToList();
    }

    /// <summary>
    /// Populates the IsConnected property on ShoppingLocationDto instances.
    /// For plugins that don't require OAuth, IsConnected is always true.
    /// For OAuth plugins, checks the TenantIntegrationTokens table for valid tokens.
    /// </summary>
    private async Task PopulateIsConnectedAsync(
        IEnumerable<ShoppingLocationDto> dtos,
        CancellationToken cancellationToken)
    {
        // Get unique integration types that have shopping locations
        var integrationTypes = dtos
            .Where(d => !string.IsNullOrEmpty(d.IntegrationType))
            .Select(d => d.IntegrationType!)
            .Distinct()
            .ToList();

        if (integrationTypes.Count == 0)
            return;

        // Map integration type -> loaded plugin (for availability + OAuth detection).
        var pluginsById = _pluginLoader.Plugins
            .OfType<IStoreIntegrationPlugin>()
            .Where(p => integrationTypes.Contains(p.PluginId))
            .GroupBy(p => p.PluginId)
            .ToDictionary(g => g.Key, g => g.First());

        // Only plugins that expose a user OAuth cart link need a token.
        var cartLinkTypes = pluginsById.Values
            .Where(p => p is IOAuthClientAuthentication && p.Capabilities.HasShoppingCart)
            .Select(p => p.PluginId)
            .ToHashSet();

        var cartLinkedPlugins = new HashSet<string>();
        var reauthPlugins = new HashSet<string>();
        if (cartLinkTypes.Count > 0)
        {
            var tokens = await _context.TenantIntegrationTokens
                .Where(t => cartLinkTypes.Contains(t.PluginId))
                .ToListAsync(cancellationToken);

            cartLinkedPlugins = tokens
                .Where(t => !string.IsNullOrEmpty(t.AccessToken) &&
                            !t.RequiresReauth &&
                            t.ExpiresAt.HasValue &&
                            t.ExpiresAt.Value > DateTime.UtcNow)
                .Select(t => t.PluginId)
                .ToHashSet();

            reauthPlugins = tokens
                .Where(t => t.RequiresReauth ||
                            (t.ExpiresAt.HasValue && t.ExpiresAt.Value <= DateTime.UtcNow))
                .Select(t => t.PluginId)
                .ToHashSet();
        }

        foreach (var dto in dtos)
        {
            if (string.IsNullOrEmpty(dto.IntegrationType))
                continue;

            var hasPlugin = pluginsById.TryGetValue(dto.IntegrationType, out var plugin);

            // Product / price / availability features work with client credentials,
            // so a linked store is "connected" whenever its plugin is available.
            dto.IsConnected = hasPlugin && plugin!.IsAvailable;
            dto.SupportsCartLink = cartLinkTypes.Contains(dto.IntegrationType);
            dto.CartLinked = cartLinkedPlugins.Contains(dto.IntegrationType);
            dto.RequiresReauth = dto.SupportsCartLink &&
                                 reauthPlugins.Contains(dto.IntegrationType) &&
                                 !dto.CartLinked;
        }
    }
}
