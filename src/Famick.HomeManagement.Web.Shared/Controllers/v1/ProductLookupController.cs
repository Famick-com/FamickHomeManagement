using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.DTOs.StoreIntegrations;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for product lookup functionality
/// </summary>
[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductLookupController : ApiControllerBase
{
    private readonly IProductLookupService _lookupService;
    private readonly IUnifiedProductSearchService _unifiedSearchService;
    private readonly IPluginLoader _pluginLoader;
    private readonly HomeManagementDbContext _dbContext;

    public ProductLookupController(
        IProductLookupService lookupService,
        IUnifiedProductSearchService unifiedSearchService,
        IPluginLoader pluginLoader,
        HomeManagementDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<ProductLookupController> logger)
        : base(tenantProvider, logger)
    {
        _lookupService = lookupService;
        _unifiedSearchService = unifiedSearchService;
        _pluginLoader = pluginLoader;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Search for products across local, master catalog, external plugins, and store
    /// integrations. Auto-detects if query is a barcode (8-14 digits) or product name.
    /// Delegates to the unified search service; <see cref="ProductLookupRequest.SearchMode"/>
    /// maps onto explicit source overrides so the historical mode semantics are preserved.
    /// Store-specific fields (price/aisle/stock) now win over other sources on merged rows.
    /// </summary>
    [HttpPost("lookup")]
    [ProducesResponseType(typeof(ProductLookupResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Search(
        [FromBody] ProductLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error_message = "Query is required" });
        }

        _logger.LogInformation("Product lookup search: {Query}, Mode: {SearchMode}, PreferredStore: {PreferredStore}",
            request.Query, request.SearchMode, request.PreferredShoppingLocationId);

        var hasStore = request.PreferredShoppingLocationId.HasValue && request.IncludeStoreResults;

        // Map the legacy SearchMode onto explicit source toggles. Master catalog stays OFF
        // here to preserve historical /lookup behavior for already-deployed clients — new
        // callers get master via the unified endpoint's General context.
        var unified = new UnifiedProductSearchRequest
        {
            Query = request.Query,
            MaxResults = request.MaxResults,
            Context = ProductSearchContext.General,
            ShoppingLocationId = request.PreferredShoppingLocationId,
            IncludeMaster = false,
            IncludeLocal = request.SearchMode is ProductSearchMode.AllSources or ProductSearchMode.LocalProductsOnly,
            IncludePlugins = request.SearchMode is ProductSearchMode.AllSources or ProductSearchMode.ExternalSourcesOnly,
            IncludeStore = hasStore && request.SearchMode is not ProductSearchMode.LocalProductsOnly,
        };

        var results = await _unifiedSearchService.SearchAsync(unified, cancellationToken);
        return ApiResponse(new ProductLookupResponse { Results = results });
    }

    /// <summary>
    /// Apply a lookup result to an existing product
    /// </summary>
    [HttpPost("{id}/apply-lookup")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductNutritionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ApplyLookupResult(
        Guid id,
        [FromBody] ApplyLookupResultRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.DataSources.Any())
        {
            return BadRequest(new { error_message = "DataSources is required" });
        }

        foreach(var dataSource in request.DataSources)
        {
            if (string.IsNullOrEmpty(dataSource.Key))
            {
                return BadRequest(new { error_message = "Datasource key is required"});
            }
        }

        _logger.LogInformation("Applying lookup result to product {ProductId}"
            , id);

        // Get the primary data source name for image attribution
        var primarySourceName = request.DataSources.Keys.FirstOrDefault() ?? "Unknown";

        // Convert request to ProductLookupResult
        var lookupResult = new ProductLookupResult
        {
            DataSources = request.DataSources,
            Name = request.Name ?? string.Empty,
            BrandName = request.BrandName,
            BrandOwner = request.BrandOwner,
            Barcodes = request.Barcodes,
            ServingSizeDescription = request.ServingSizeDescription,
            Ingredients = request.Ingredients,
            ImageUrl = !string.IsNullOrEmpty(request.ImageUrl)
                ? new ResultImage { ImageUrl = request.ImageUrl, PluginId = primarySourceName }
                : null,
            ThumbnailUrl = !string.IsNullOrEmpty(request.ThumbnailUrl)
                ? new ResultImage { ImageUrl = request.ThumbnailUrl, PluginId = primarySourceName }
                : null,
            AttributionMarkdown = request.AttributionMarkdown,
            Nutrition = request.Nutrition != null ? new ProductLookupNutrition
            {
                Source = request.Nutrition.DataSource,
                ServingSize = request.Nutrition.ServingSize,
                ServingUnit = request.Nutrition.ServingUnit,
                Calories = request.Nutrition.Calories,
                TotalFat = request.Nutrition.TotalFat,
                SaturatedFat = request.Nutrition.SaturatedFat,
                TransFat = request.Nutrition.TransFat,
                Cholesterol = request.Nutrition.Cholesterol,
                Sodium = request.Nutrition.Sodium,
                TotalCarbohydrates = request.Nutrition.TotalCarbohydrates,
                DietaryFiber = request.Nutrition.DietaryFiber,
                TotalSugars = request.Nutrition.TotalSugars,
                AddedSugars = request.Nutrition.AddedSugars,
                Protein = request.Nutrition.Protein,
                VitaminA = request.Nutrition.VitaminA,
                VitaminC = request.Nutrition.VitaminC,
                VitaminD = request.Nutrition.VitaminD,
                VitaminE = request.Nutrition.VitaminE,
                VitaminK = request.Nutrition.VitaminK,
                Thiamin = request.Nutrition.Thiamin,
                Riboflavin = request.Nutrition.Riboflavin,
                Niacin = request.Nutrition.Niacin,
                VitaminB6 = request.Nutrition.VitaminB6,
                Folate = request.Nutrition.Folate,
                VitaminB12 = request.Nutrition.VitaminB12,
                Calcium = request.Nutrition.Calcium,
                Iron = request.Nutrition.Iron,
                Magnesium = request.Nutrition.Magnesium,
                Phosphorus = request.Nutrition.Phosphorus,
                Potassium = request.Nutrition.Potassium,
                Zinc = request.Nutrition.Zinc
            } : null
        };

        try
        {
            await _lookupService.ApplyLookupResultAsync(id, lookupResult, cancellationToken);

            // Return the updated nutrition data
            var nutrition = await _dbContext.ProductNutrition
                .FirstOrDefaultAsync(pn => pn.ProductId == id && pn.TenantId == TenantId, cancellationToken);

            if (nutrition == null)
            {
                return NotFoundResponse("Nutrition data not found after apply");
            }

            return ApiResponse(MapToDto(nutrition));
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    /// <summary>
    /// Get nutrition data for a product
    /// </summary>
    [HttpGet("{id}/nutrition")]
    [ProducesResponseType(typeof(ProductNutritionDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetNutrition(Guid id, CancellationToken cancellationToken)
    {
        var nutrition = await _dbContext.ProductNutrition
            .FirstOrDefaultAsync(pn => pn.ProductId == id && pn.TenantId == TenantId, cancellationToken);

        if (nutrition == null)
        {
            return NotFoundResponse($"Nutrition data not found for product {id}");
        }

        return ApiResponse(MapToDto(nutrition));
    }

    /// <summary>
    /// Update nutrition data for a product
    /// </summary>
    [HttpPut("{id}/nutrition")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductNutritionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateNutrition(
        Guid id,
        [FromBody] ProductNutritionDto dto,
        CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, cancellationToken);

        if (product == null)
        {
            return NotFoundResponse($"Product with ID {id} not found");
        }

        var nutrition = await _dbContext.ProductNutrition
            .FirstOrDefaultAsync(pn => pn.ProductId == id && pn.TenantId == TenantId, cancellationToken);

        if (nutrition == null)
        {
            nutrition = new ProductNutrition
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProductId = id
            };
            _dbContext.ProductNutrition.Add(nutrition);
        }

        // Update fields
        nutrition.ExternalId = dto.ExternalId;
        nutrition.DataSource = dto.DataSource;
        nutrition.ServingSize = dto.ServingSize;
        nutrition.ServingUnit = dto.ServingUnit;
        nutrition.ServingSizeDescription = dto.ServingSizeDescription;
        nutrition.ServingsPerContainer = dto.ServingsPerContainer;
        nutrition.Calories = dto.Calories;
        nutrition.TotalFat = dto.TotalFat;
        nutrition.SaturatedFat = dto.SaturatedFat;
        nutrition.TransFat = dto.TransFat;
        nutrition.Cholesterol = dto.Cholesterol;
        nutrition.Sodium = dto.Sodium;
        nutrition.TotalCarbohydrates = dto.TotalCarbohydrates;
        nutrition.DietaryFiber = dto.DietaryFiber;
        nutrition.TotalSugars = dto.TotalSugars;
        nutrition.AddedSugars = dto.AddedSugars;
        nutrition.Protein = dto.Protein;
        nutrition.VitaminA = dto.VitaminA;
        nutrition.VitaminC = dto.VitaminC;
        nutrition.VitaminD = dto.VitaminD;
        nutrition.VitaminE = dto.VitaminE;
        nutrition.VitaminK = dto.VitaminK;
        nutrition.Thiamin = dto.Thiamin;
        nutrition.Riboflavin = dto.Riboflavin;
        nutrition.Niacin = dto.Niacin;
        nutrition.VitaminB6 = dto.VitaminB6;
        nutrition.Folate = dto.Folate;
        nutrition.VitaminB12 = dto.VitaminB12;
        nutrition.Calcium = dto.Calcium;
        nutrition.Iron = dto.Iron;
        nutrition.Magnesium = dto.Magnesium;
        nutrition.Phosphorus = dto.Phosphorus;
        nutrition.Potassium = dto.Potassium;
        nutrition.Zinc = dto.Zinc;
        nutrition.BrandOwner = dto.BrandOwner;
        nutrition.BrandName = dto.BrandName;
        nutrition.Ingredients = dto.Ingredients;
        nutrition.LastUpdatedFromSource = dto.LastUpdatedFromSource ?? DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse(MapToDto(nutrition));
    }

    /// <summary>
    /// List available plugins
    /// </summary>
    [HttpGet("~/api/v1/plugins")]
    [ProducesResponseType(typeof(List<PluginInfo>), 200)]
    [ProducesResponseType(401)]
    public IActionResult ListPlugins()
    {
        var plugins = _lookupService.GetAvailablePlugins();
        return ApiResponse(plugins);
    }

    /// <summary>
    /// Reload plugins from configuration
    /// </summary>
    [HttpPost("~/api/v1/plugins/reload")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(List<PluginInfo>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ReloadPlugins(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reloading plugins");

        await _pluginLoader.LoadPluginsAsync(cancellationToken);

        var plugins = _lookupService.GetAvailablePlugins();
        return ApiResponse(plugins);
    }

    private static ProductNutritionDto MapToDto(ProductNutrition entity)
    {
        return new ProductNutritionDto
        {
            Id = entity.Id,
            ProductId = entity.ProductId,
            ExternalId = entity.ExternalId,
            DataSource = entity.DataSource,
            ServingSize = entity.ServingSize,
            ServingUnit = entity.ServingUnit,
            ServingSizeDescription = entity.ServingSizeDescription,
            ServingsPerContainer = entity.ServingsPerContainer,
            Calories = entity.Calories,
            TotalFat = entity.TotalFat,
            SaturatedFat = entity.SaturatedFat,
            TransFat = entity.TransFat,
            Cholesterol = entity.Cholesterol,
            Sodium = entity.Sodium,
            TotalCarbohydrates = entity.TotalCarbohydrates,
            DietaryFiber = entity.DietaryFiber,
            TotalSugars = entity.TotalSugars,
            AddedSugars = entity.AddedSugars,
            Protein = entity.Protein,
            VitaminA = entity.VitaminA,
            VitaminC = entity.VitaminC,
            VitaminD = entity.VitaminD,
            VitaminE = entity.VitaminE,
            VitaminK = entity.VitaminK,
            Thiamin = entity.Thiamin,
            Riboflavin = entity.Riboflavin,
            Niacin = entity.Niacin,
            VitaminB6 = entity.VitaminB6,
            Folate = entity.Folate,
            VitaminB12 = entity.VitaminB12,
            Calcium = entity.Calcium,
            Iron = entity.Iron,
            Magnesium = entity.Magnesium,
            Phosphorus = entity.Phosphorus,
            Potassium = entity.Potassium,
            Zinc = entity.Zinc,
            BrandOwner = entity.BrandOwner,
            BrandName = entity.BrandName,
            Ingredients = entity.Ingredients,
            LastUpdatedFromSource = entity.LastUpdatedFromSource
        };
    }
}
