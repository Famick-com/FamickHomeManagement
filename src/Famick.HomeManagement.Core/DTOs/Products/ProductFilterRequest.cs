namespace Famick.HomeManagement.Core.DTOs.Products;

public class ProductFilterRequest
{
    public string? SearchTerm { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ProductGroupId { get; set; }  // Phase 2
    public Guid? ShoppingLocationId { get; set; }  // Phase 2
    public bool? IsActive { get; set; }
    public bool? LowStock { get; set; }  // Phase 2: Filter products below MinStockAmount
    public int? Page { get; set; }
    public int PageSize { get; set; } = 25;
    public string? SortBy { get; set; } = "Name";
    public bool Descending { get; set; } = false;

    /// <summary>
    /// Orders generic products (<see cref="ProductDto.IsGeneric"/>) before specific ones, then by how
    /// closely the name matches <see cref="SearchTerm"/>, then by name. Used by recipe ingredient
    /// search; takes the place of <see cref="SortBy"/> when set. With a <see cref="SearchTerm"/>, an
    /// unpaged list also has the catalog's matching products the household doesn't have
    /// (<see cref="ProductDto.IsCatalogProduct"/>): the household's generic products, the catalog's,
    /// the household's specific ones, then the catalog's.
    /// </summary>
    public bool GenericFirst { get; set; }
}
