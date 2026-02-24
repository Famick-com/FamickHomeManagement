namespace Famick.HomeManagement.Core.DTOs.Stock;

/// <summary>
/// Request for quick consume actions (no dialog).
/// </summary>
public class QuickConsumeRequest
{
    /// <summary>
    /// Product ID to consume from.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Amount to consume. Defaults to 1.
    /// </summary>
    public decimal Amount { get; set; } = 1;

    /// <summary>
    /// If true, consume all remaining stock for this product.
    /// </summary>
    public bool ConsumeAll { get; set; }

    /// <summary>
    /// If true, mark the consumed stock as spoiled.
    /// </summary>
    public bool Spoiled { get; set; }
}
