using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Data;

/// <summary>
/// Seeds default data for locations and quantity units
/// </summary>
public class DataSeeder
{
    private readonly HomeManagementDbContext _dbContext;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(HomeManagementDbContext dbContext, ILogger<DataSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Seeds default data for a specific tenant if not already present
    /// </summary>
    public async Task SeedDefaultDataAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await SeedLocationsAsync(tenantId, cancellationToken);
        await SeedQuantityUnitsAsync(tenantId, cancellationToken);
    }

    private async Task SeedLocationsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var hasLocations = await _dbContext.Locations
            .AnyAsync(l => l.TenantId == tenantId, cancellationToken);

        if (hasLocations)
        {
            _logger.LogDebug("Locations already seeded for tenant {TenantId}", tenantId);
            return;
        }

        _logger.LogInformation("Seeding default locations for tenant {TenantId}", tenantId);

        var locations = new List<Location>
        {
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kitchen", Description = "Main kitchen area", SortOrder = 1, IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pantry", Description = "Dry goods storage", SortOrder = 2, IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Refrigerator", Description = "Main refrigerator", SortOrder = 3, IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Freezer", Description = "Freezer compartment", SortOrder = 4, IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Basement", Description = "Basement storage", SortOrder = 5, IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Garage", Description = "Garage storage", SortOrder = 6, IsActive = true },
        };

        _dbContext.Locations.AddRange(locations);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} default locations for tenant {TenantId}", locations.Count, tenantId);
    }

    private async Task SeedQuantityUnitsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var hasUnits = await _dbContext.QuantityUnits
            .AnyAsync(u => u.TenantId == tenantId, cancellationToken);

        if (hasUnits)
        {
            _logger.LogDebug("Quantity units already seeded for tenant {TenantId}", tenantId);
            return;
        }

        _logger.LogInformation("Seeding default quantity units for tenant {TenantId}", tenantId);

        var units = new List<QuantityUnit>
        {
            // Count units
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Piece", NamePlural = "Pieces", Description = "Individual items", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pack", NamePlural = "Packs", Description = "Packaged items", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Box", NamePlural = "Boxes", Description = "Boxed items", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Can", NamePlural = "Cans", Description = "Canned items", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bottle", NamePlural = "Bottles", Description = "Bottled items", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Jar", NamePlural = "Jars", Description = "Jarred items", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bag", NamePlural = "Bags", Description = "Bagged items", IsActive = true },

            // Weight units
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kilogram", NamePlural = "Kilograms", Description = "kg - metric weight", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Gram", NamePlural = "Grams", Description = "g - metric weight", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pound", NamePlural = "Pounds", Description = "lb - imperial weight", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Ounce", NamePlural = "Ounces", Description = "oz - imperial weight", IsActive = true },

            // Volume units
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Liter", NamePlural = "Liters", Description = "L - metric volume", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Milliliter", NamePlural = "Milliliters", Description = "mL - metric volume", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Gallon", NamePlural = "Gallons", Description = "gal - imperial volume", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Quart", NamePlural = "Quarts", Description = "qt - imperial volume", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pint", NamePlural = "Pints", Description = "pt - imperial volume", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cup", NamePlural = "Cups", Description = "cooking measurement", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Tablespoon", NamePlural = "Tablespoons", Description = "tbsp - cooking measurement", IsActive = true },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Teaspoon", NamePlural = "Teaspoons", Description = "tsp - cooking measurement", IsActive = true },
        };

        _dbContext.QuantityUnits.AddRange(units);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} default quantity units for tenant {TenantId}", units.Count, tenantId);
    }
}
