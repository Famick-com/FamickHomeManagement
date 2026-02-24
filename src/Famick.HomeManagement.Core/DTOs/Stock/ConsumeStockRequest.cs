using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Request to consume (use/remove) stock.
/// </summary>
public class ConsumeStockRequest
{
    /// <summary>
    /// Amount to consume. If null or equal to current amount, the entire entry is consumed.
    /// </summary>
    [Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal? Amount { get; set; }

    /// <summary>
    /// Whether the stock was spoiled (expired/bad).
    /// </summary>
    public bool Spoiled { get; set; }

    /// <summary>
    /// Optional recipe ID if consumed for a recipe.
    /// </summary>
    public Guid? RecipeId { get; set; }
}
