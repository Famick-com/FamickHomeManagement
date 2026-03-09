using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ProductOnboardingService : IProductOnboardingService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProductOnboardingService> _logger;

    private const string MasterProductCacheKey = "master-products-all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProductOnboardingService(
        HomeManagementDbContext context,
        IMemoryCache cache,
        ILogger<ProductOnboardingService> logger)
    {
        _context = context;
        _cache = cache;
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

    public async Task<ProductOnboardingPreviewResponse> PreviewAsync(
        ProductOnboardingAnswersDto answers, CancellationToken ct = default)
    {
        var allMasterProducts = await GetCachedMasterProductsAsync(ct);
        var filtered = FilterMasterProducts(allMasterProducts, answers);

        var grouped = filtered
            .GroupBy(mp => mp.Category)
            .OrderBy(g => g.Key)
            .Select(g => new MasterProductCategoryGroup
            {
                Category = g.Key,
                ItemCount = g.Count(),
                Items = g.OrderByDescending(mp => mp.Popularity)
                    .ThenBy(mp => mp.Name)
                    .Select(mp => new MasterProductDto
                    {
                        Id = mp.Id,
                        Name = mp.Name,
                        Category = mp.Category,
                        ContainerType = mp.ContainerType,
                        IsStaple = mp.IsStaple
                    }).ToList()
            }).ToList();

        return new ProductOnboardingPreviewResponse
        {
            TotalMasterProducts = allMasterProducts.Count,
            FilteredCount = filtered.Count,
            Categories = grouped
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

        if (masterProducts.Count == 0)
        {
            _logger.LogWarning("No master products found for the provided IDs");
            return new ProductOnboardingCompleteResponse { ProductsCreated = 0, ProductsSkipped = 0 };
        }

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
        var productsToCreate = new List<Product>();
        var skippedCount = 0;

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

    private async Task<List<MasterProduct>> GetCachedMasterProductsAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(MasterProductCacheKey, out List<MasterProduct>? cached) && cached != null)
            return cached;

        var masterProducts = await _context.MasterProducts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(ct);

        _cache.Set(MasterProductCacheKey, masterProducts, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheDuration
        });

        return masterProducts;
    }

    private static List<MasterProduct> FilterMasterProducts(
        List<MasterProduct> masterProducts, ProductOnboardingAnswersDto answers)
    {
        var result = new List<MasterProduct>();

        foreach (var mp in masterProducts)
        {
            var lifestyleTags = DeserializeTags(mp.LifestyleTags);

            // Lifestyle filtering: items with lifestyle tags are only included
            // if the corresponding toggle is enabled
            if (lifestyleTags.Count > 0)
            {
                var included = false;
                if (answers.HasBaby && lifestyleTags.Contains("baby")) included = true;
                if (answers.HasPets && lifestyleTags.Contains("pet")) included = true;
                if (answers.TrackHouseholdSupplies && lifestyleTags.Contains("household")) included = true;
                if (answers.TrackPersonalCare && lifestyleTags.Contains("personal-care")) included = true;
                if (answers.TrackPharmacy && lifestyleTags.Contains("pharmacy")) included = true;

                if (!included) continue;
            }

            // Dietary exclusion
            if (answers.DietaryPreferences.Count > 0)
            {
                var conflictFlags = DeserializeTags(mp.DietaryConflictFlags);
                var selectedPreferenceNames = answers.DietaryPreferences.Select(p => p.ToString()).ToHashSet();
                if (conflictFlags.Any(f => selectedPreferenceNames.Contains(f)))
                    continue;
            }

            // Allergen exclusion
            if (answers.Allergens.Count > 0)
            {
                var allergenFlags = DeserializeTags(mp.AllergenFlags);
                var selectedAllergenNames = answers.Allergens.Select(a => a.ToString()).ToHashSet();
                if (allergenFlags.Any(f => selectedAllergenNames.Contains(f)))
                    continue;
            }

            // Cooking style filtering
            if (answers.CookingStyles.Count > 0)
            {
                var cookingTags = DeserializeTags(mp.CookingStyleTags);
                // Items with no cooking style tags always pass (staples/basics)
                if (cookingTags.Count > 0)
                {
                    var selectedStyleNames = answers.CookingStyles.Select(s => s.ToString()).ToHashSet();
                    if (!cookingTags.Any(t => selectedStyleNames.Contains(t)))
                        continue;
                }
            }

            result.Add(mp);
        }

        return result;
    }

    private static HashSet<string> DeserializeTags(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]")
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(json);
            return tags != null
                ? new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
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
