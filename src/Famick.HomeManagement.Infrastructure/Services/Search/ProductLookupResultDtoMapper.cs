using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;

namespace Famick.HomeManagement.Infrastructure.Services.Search;

/// <summary>
/// Maps the internal pipeline/merge type (<see cref="ProductLookupResult"/>) to the API
/// response DTO (<see cref="ProductLookupResultDto"/>). Centralized so the unified search
/// service and the legacy endpoint shims produce identical shapes, including the per-source
/// <see cref="ProductLookupResultDto.Sources"/> list and <c>MasterProductId</c>.
/// </summary>
public static class ProductLookupResultDtoMapper
{
    public static ProductLookupResultDto ToDto(ProductLookupResult r)
    {
        var sourceNames = string.Join(", ", r.DataSources.Keys);
        var primarySource = r.DataSources.FirstOrDefault();

        var isLocalProduct = r.DataSources.ContainsKey(ProductSearchService.LocalProductsDataSource);
        Guid? localProductId = null;
        if (isLocalProduct &&
            r.DataSources.TryGetValue(ProductSearchService.LocalProductsDataSource, out var localIdStr) &&
            Guid.TryParse(localIdStr, out var parsedLocalId))
        {
            localProductId = parsedLocalId;
        }

        Guid? masterProductId = null;
        if (r.DataSources.TryGetValue(ProductSearchService.MasterCatalogDataSource, out var masterIdStr) &&
            Guid.TryParse(masterIdStr, out var parsedMasterId))
        {
            masterProductId = parsedMasterId;
        }

        // Top-level source type: local wins, else store (has store fields), else plugin.
        string sourceType;
        if (isLocalProduct)
            sourceType = "LocalProduct";
        else if (masterProductId.HasValue && !HasStoreFields(r))
            sourceType = "MasterCatalog";
        else if (HasStoreFields(r))
            sourceType = "StoreIntegration";
        else
            sourceType = "ProductPlugin";

        return new ProductLookupResultDto
        {
            SourceType = sourceType,
            PluginId = primarySource.Key ?? string.Empty,
            PluginDisplayName = sourceNames,
            Sources = r.DataSources.Select(kvp => new ResultSourceDto
            {
                PluginId = kvp.Key,
                DisplayName = kvp.Key,
                SourceType = SourceTypeForKey(kvp.Key),
                ExternalId = kvp.Value
            }).ToList(),
            ExternalId = primarySource.Value ?? string.Empty,
            LocalProductId = localProductId,
            IsLocalProduct = isLocalProduct,
            MasterProductId = masterProductId,
            Name = r.Name,
            Brand = r.BrandName,
            Barcodes = r.Barcodes.ToList(),
            OriginalSearchBarcode = r.OriginalSearchBarcode?.Data,
            Category = r.Categories.FirstOrDefault(),
            ImageUrl = r.ImageUrl?.ImageUrl,
            ThumbnailUrl = r.ThumbnailUrl?.ImageUrl,
            Nutrition = r.Nutrition,
            Ingredients = r.Ingredients,
            ServingSizeDescription = r.ServingSizeDescription,
            BrandOwner = r.BrandOwner,

            // Store-specific fields
            Price = r.Price,
            PriceUnit = r.PriceUnit,
            SalePrice = r.SalePrice,
            Aisle = r.Aisle,
            Shelf = r.Shelf,
            Department = r.Department,
            InStock = r.InStock,
            Size = r.Size,
            ProductUrl = r.ProductUrl,
            ShoppingLocationId = r.ShoppingLocationId,
            ShoppingLocationName = r.ShoppingLocationName,
            AttributionMarkdown = r.AttributionMarkdown,
        };
    }

    private static bool HasStoreFields(ProductLookupResult r) =>
        r.Price.HasValue || !string.IsNullOrEmpty(r.Aisle) || !string.IsNullOrEmpty(r.Department);

    private static string SourceTypeForKey(string key)
    {
        if (key == ProductSearchService.LocalProductsDataSource) return "LocalProduct";
        if (key == ProductSearchService.MasterCatalogDataSource) return "MasterCatalog";
        return "External";
    }
}
