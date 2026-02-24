namespace Famick.HomeManagement.Core.DTOs.Products;

/// <summary>
/// Request to add an image to a product from a URL
/// </summary>
public class AddImageFromUrlRequest
{
    /// <summary>
    /// The URL of the image to add
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;
}
