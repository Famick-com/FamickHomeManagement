using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// A result from the shopping-list product search.
/// <para>
/// The search blends three sources — the linked store's catalogue, the household's own
/// products, and the global master catalogue — so the response needs to say which one a
/// row came from. <see cref="StoreProductResult"/> alone cannot: it is a plugin-contract
/// type describing a product *at a store*, with no notion of a master-catalogue identity.
/// Returning the bare plugin type meant master rows reached the client stripped of their
/// id, and selecting one created an unlinked duplicate product instead of materializing
/// the master — which then never deduplicated on later searches.
/// </para>
/// </summary>
public class ShoppingListProductSearchResult : StoreProductResult
{
    /// <summary>
    /// Master-catalogue product id when this row is a global (not yet materialized)
    /// product. Clients pass it to products/from-master on selection.
    /// </summary>
    public Guid? MasterProductId { get; set; }

    /// <summary>True when this row is a global master-catalogue product.</summary>
    public bool IsMasterProduct { get; set; }

    /// <summary>
    /// Id of the household's own product when this row is already in the tenant catalogue,
    /// so the client can attach to it directly rather than creating another.
    /// </summary>
    public Guid? LocalProductId { get; set; }

    /// <summary>True when this row is one of the household's own products.</summary>
    public bool IsLocalProduct { get; set; }

    /// <summary>
    /// Copies the fields of a store-integration result onto the richer type, so store,
    /// local and master rows all come back through one shape.
    /// </summary>
    public static ShoppingListProductSearchResult FromStoreResult(StoreProductResult source) => new()
    {
        ExternalProductId = source.ExternalProductId,
        Name = source.Name,
        Brand = source.Brand,
        Barcodes = source.Barcodes,
        ImageUrl = source.ImageUrl,
        Price = source.Price,
        PriceUnit = source.PriceUnit,
        SalePrice = source.SalePrice,
        Aisle = source.Aisle,
        Shelf = source.Shelf,
        Department = source.Department,
        InStock = source.InStock,
        Size = source.Size,
        ProductUrl = source.ProductUrl,
        Categories = source.Categories,
        Description = source.Description,
        CacheDuration = source.CacheDuration
    };
}
