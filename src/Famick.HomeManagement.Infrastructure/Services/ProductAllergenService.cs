using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ProductAllergenService : IProductAllergenService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<ProductAllergenService> _logger;

    public ProductAllergenService(
        HomeManagementDbContext context,
        ILogger<ProductAllergenService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductAllergenTagsDto> GetAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await _context.Products
            .Include(p => p.Allergens)
            .Include(p => p.DietaryConflicts)
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new KeyNotFoundException($"Product with ID {productId} not found");

        return new ProductAllergenTagsDto
        {
            ProductId = productId,
            Allergens = product.Allergens.Select(a => a.AllergenType).ToList(),
            DietaryConflicts = product.DietaryConflicts.Select(dc => dc.DietaryPreference).ToList()
        };
    }

    public async Task<ProductAllergenTagsDto> UpdateAsync(
        Guid productId, UpdateProductAllergenTagsRequest request, CancellationToken ct = default)
    {
        var product = await _context.Products
            .Include(p => p.Allergens)
            .Include(p => p.DietaryConflicts)
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new KeyNotFoundException($"Product with ID {productId} not found");

        // Full replacement of allergens
        _context.ProductAllergens.RemoveRange(product.Allergens);
        product.Allergens.Clear();
        foreach (var allergenType in request.Allergens.Distinct())
        {
            product.Allergens.Add(new ProductAllergen
            {
                ProductId = productId,
                AllergenType = allergenType
            });
        }

        // Full replacement of dietary conflicts
        _context.ProductDietaryConflicts.RemoveRange(product.DietaryConflicts);
        product.DietaryConflicts.Clear();
        foreach (var pref in request.DietaryConflicts.Distinct())
        {
            product.DietaryConflicts.Add(new ProductDietaryConflict
            {
                ProductId = productId,
                DietaryPreference = pref
            });
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated allergen tags for product {ProductId}", productId);

        return new ProductAllergenTagsDto
        {
            ProductId = productId,
            Allergens = product.Allergens.Select(a => a.AllergenType).ToList(),
            DietaryConflicts = product.DietaryConflicts.Select(dc => dc.DietaryPreference).ToList()
        };
    }
}
