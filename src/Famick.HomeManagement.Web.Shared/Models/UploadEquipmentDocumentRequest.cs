using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Models;

/// <summary>
/// Request model for uploading a document to equipment.
/// Combines file upload with metadata in a single form request.
/// </summary>
public class UploadEquipmentDocumentRequest
{
    /// <summary>
    /// The file to upload
    /// </summary>
    [FromForm(Name = "file")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Optional display name for the document
    /// </summary>
    [FromForm(Name = "displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional tag ID to categorize the document
    /// </summary>
    [FromForm(Name = "tagId")]
    public Guid? TagId { get; set; }
}
