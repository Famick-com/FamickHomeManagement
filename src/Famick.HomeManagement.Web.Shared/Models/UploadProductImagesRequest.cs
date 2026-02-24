using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Models;

/// <summary>
/// Request model for uploading images to a product.
/// </summary>
public class UploadProductImagesRequest
{
    /// <summary>
    /// The image files to upload
    /// </summary>
    [FromForm(Name = "files")]
    public List<IFormFile> Files { get; set; } = new();
}
