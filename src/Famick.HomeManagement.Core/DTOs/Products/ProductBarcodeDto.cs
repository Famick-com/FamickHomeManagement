namespace Famick.HomeManagement.Core.DTOs.Products;

public class ProductBarcodeDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
