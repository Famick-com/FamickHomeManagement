namespace Famick.HomeManagement.Core.DTOs.Products;

public class CreateProductFromLookupRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? Barcode { get; set; }
    public string? OriginalSearchBarcode { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ExternalId { get; set; }
    public string? SourceType { get; set; }
    public string? PluginId { get; set; }
    public Guid? ShoppingLocationId { get; set; }
    public string? Aisle { get; set; }
    public string? Shelf { get; set; }
    public string? Department { get; set; }
    public decimal? Price { get; set; }
}
