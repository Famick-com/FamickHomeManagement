using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ProductOnboardingService : IProductOnboardingService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<ProductOnboardingService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProductOnboardingService(
        HomeManagementDbContext context,
        ILogger<ProductOnboardingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductOnboardingStateDto> GetStateAsync(Guid tenantId, CancellationToken ct = default)
    {
        var state = await _context.TenantProductOnboardingStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (state == null)
        {
            return new ProductOnboardingStateDto
            {
                HasCompletedOnboarding = false,
                ProductsCreatedCount = 0
            };
        }

        ProductOnboardingAnswersDto? savedAnswers = null;
        if (!string.IsNullOrEmpty(state.QuestionnaireAnswersJson))
        {
            try
            {
                savedAnswers = JsonSerializer.Deserialize<ProductOnboardingAnswersDto>(
                    state.QuestionnaireAnswersJson, JsonOptions);
            }
            catch
            {
                _logger.LogWarning("Failed to deserialize saved questionnaire answers for tenant {TenantId}", tenantId);
            }
        }

        return new ProductOnboardingStateDto
        {
            HasCompletedOnboarding = state.HasCompletedOnboarding,
            CompletedAt = state.CompletedAt,
            ProductsCreatedCount = state.ProductsCreatedCount,
            SavedAnswers = savedAnswers
        };
    }

    public async Task<ProductOnboardingCompleteResponse> CompleteAsync(
        Guid tenantId, ProductOnboardingCompleteRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting product onboarding completion for tenant {TenantId} with {Count} selected master products",
            tenantId, request.SelectedMasterProductIds.Count);

        // Load selected master products
        var selectedIds = request.SelectedMasterProductIds.ToHashSet();
        var masterProducts = await _context.MasterProducts
            .IgnoreQueryFilters()
            .Where(mp => selectedIds.Contains(mp.Id))
            .ToListAsync(ct);

        if (masterProducts.Count == 0 && request.SelectedMasterProductIds.Count > 0)
        {
            _logger.LogWarning("No master products found for the provided IDs");
        }

        var productsToCreate = new List<Product>();
        var skippedCount = 0;

        if (masterProducts.Count > 0)
        {
            // Load existing product names for dedup (case-insensitive)
            var existingNames = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => p.Name.ToLower())
                .ToListAsync(ct);
            var existingNameSet = existingNames.ToHashSet();

            // Load tenant's locations and quantity units for hint resolution
            var locations = await _context.Locations
                .Where(l => l.IsActive)
                .ToListAsync(ct);
            var quantityUnits = await _context.QuantityUnits
                .Where(qu => qu.IsActive)
                .ToListAsync(ct);

            // Resolve default fallbacks
            var defaultLocation = ResolveLocation(locations, "Pantry")
                ?? locations.FirstOrDefault();
            var defaultQuantityUnit = ResolveQuantityUnit(quantityUnits, "Piece")
                ?? quantityUnits.FirstOrDefault();

            if (defaultLocation == null || defaultQuantityUnit == null)
            {
                _logger.LogError("No locations or quantity units found for tenant {TenantId}. Cannot create products.", tenantId);
                throw new InvalidOperationException("Tenant has no locations or quantity units configured. Please complete initial setup first.");
            }

            // Find or create ProductGroups per category
            var existingGroups = await _context.ProductGroups.ToListAsync(ct);
            var groupsByName = existingGroups.ToDictionary(g => g.Name, g => g, StringComparer.OrdinalIgnoreCase);
            var newGroups = new List<ProductGroup>();

            foreach (var category in masterProducts.Select(mp => mp.Category).Distinct())
            {
                if (!groupsByName.ContainsKey(category))
                {
                    var newGroup = new ProductGroup
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Name = category
                    };
                    newGroups.Add(newGroup);
                    groupsByName[category] = newGroup;
                }
            }

            if (newGroups.Count > 0)
            {
                _context.ProductGroups.AddRange(newGroups);
            }

            // Create tenant products linked to master products (skip duplicates)
            foreach (var masterProduct in masterProducts)
            {
                if (existingNameSet.Contains(masterProduct.Name.ToLower()))
                {
                    skippedCount++;
                    continue;
                }

                var location = ResolveLocation(locations, masterProduct.DefaultLocationHint) ?? defaultLocation;
                var quantityUnit = ResolveQuantityUnit(quantityUnits, masterProduct.DefaultQuantityUnitHint) ?? defaultQuantityUnit;

                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = masterProduct.Name,
                    Description = masterProduct.Description,
                    MasterProductId = masterProduct.Id,
                    OverriddenFields = "[]",
                    LocationId = location.Id,
                    QuantityUnitIdPurchase = quantityUnit.Id,
                    QuantityUnitIdStock = quantityUnit.Id,
                    QuantityUnitFactorPurchaseToStock = 1.0m,
                    MinStockAmount = 0,
                    DefaultBestBeforeDays = masterProduct.DefaultBestBeforeDays,
                    TracksBestBeforeDate = masterProduct.TracksBestBeforeDate,
                    ServingSize = masterProduct.ServingSize,
                    ServingUnit = masterProduct.ServingUnit,
                    ServingsPerContainer = masterProduct.ServingsPerContainer,
                    DataSourceAttribution = masterProduct.DataSourceAttribution,
                    IsActive = true,
                    ProductGroupId = groupsByName.TryGetValue(masterProduct.Category, out var group) ? group.Id : null
                };

                productsToCreate.Add(product);
                existingNameSet.Add(masterProduct.Name.ToLower());
            }

            if (productsToCreate.Count > 0)
            {
                _context.Products.AddRange(productsToCreate);
            }
        }

        // Save or update onboarding state
        var state = await _context.TenantProductOnboardingStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (state == null)
        {
            state = new TenantProductOnboardingState
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId
            };
            _context.TenantProductOnboardingStates.Add(state);
        }

        state.HasCompletedOnboarding = true;
        state.CompletedAt = DateTime.UtcNow;
        state.ProductsCreatedCount = (state.ProductsCreatedCount) + productsToCreate.Count;
        state.QuestionnaireAnswersJson = JsonSerializer.Serialize(request.Answers, JsonOptions);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Product onboarding completed for tenant {TenantId}: {Created} created, {Skipped} skipped",
            tenantId, productsToCreate.Count, skippedCount);

        return new ProductOnboardingCompleteResponse
        {
            ProductsCreated = productsToCreate.Count,
            ProductsSkipped = skippedCount
        };
    }

    public async Task ResetAsync(Guid tenantId, CancellationToken ct = default)
    {
        var state = await _context.TenantProductOnboardingStates
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (state != null)
        {
            state.HasCompletedOnboarding = false;
            state.CompletedAt = null;
            await _context.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Reset product onboarding for tenant {TenantId}", tenantId);
    }

    private static Location? ResolveLocation(List<Location> locations, string? hint)
    {
        if (string.IsNullOrEmpty(hint))
            return null;

        return locations.FirstOrDefault(l =>
            l.Name.Equals(hint, StringComparison.OrdinalIgnoreCase));
    }

    private static QuantityUnit? ResolveQuantityUnit(List<QuantityUnit> units, string? hint)
    {
        if (string.IsNullOrEmpty(hint))
            return null;

        return units.FirstOrDefault(u =>
            u.Name.Equals(hint, StringComparison.OrdinalIgnoreCase));
    }
}
