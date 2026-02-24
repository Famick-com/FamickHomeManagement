using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Request to add stock entries in batch, supporting both bulk mode
/// (single entry with quantity) and individual mode (separate entry per item).
/// </summary>
public class AddStockBatchRequest
{
    [Required]
    public Guid ProductId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? ShoppingLocationId { get; set; }

    public DateTime? PurchasedDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Price { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// List of individual items with their own expiration dates.
    /// Each item creates a StockEntry with Amount=1.
    /// Mutually exclusive with BulkAmount.
    /// </summary>
    public List<IndividualStockItem>? IndividualItems { get; set; }

    /// <summary>
    /// Quantity of items without individual dates (creates single entry).
    /// Mutually exclusive with IndividualItems.
    /// </summary>
    [Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal? BulkAmount { get; set; }

    /// <summary>
    /// Best before date for bulk items (used only when BulkAmount is set).
    /// </summary>
    public DateTime? BulkBestBeforeDate { get; set; }
}

/// <summary>
/// Represents an individual stock item with its own expiration date.
/// </summary>
public class IndividualStockItem
{
    /// <summary>
    /// Best before date for this specific item.
    /// </summary>
    public DateTime? BestBeforeDate { get; set; }

    /// <summary>
    /// Optional note for this specific item.
    /// </summary>
    public string? Note { get; set; }
}
