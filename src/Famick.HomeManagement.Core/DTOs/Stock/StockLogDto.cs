namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Stock log entry for the journal display.
/// </summary>
public class StockLogDto
{
    /// <summary>
    /// Log entry ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Product ID.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Product name.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Amount involved in the transaction (positive for additions, negative for removals).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Transaction type: "purchase", "consume", "inventory-correction", "product-opened", etc.
    /// </summary>
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>
    /// When the transaction occurred.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Location name where the transaction occurred.
    /// </summary>
    public string? LocationName { get; set; }

    /// <summary>
    /// Optional note on the transaction.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// True if the stock was marked as spoiled.
    /// </summary>
    public bool Spoiled { get; set; }

    /// <summary>
    /// The recipe the stock was used for, when it was consumed by making one.
    /// </summary>
    public Guid? RecipeId { get; set; }

    /// <summary>
    /// That recipe's name, or null when there is none or it has since been deleted.
    /// </summary>
    public string? RecipeName { get; set; }
}
