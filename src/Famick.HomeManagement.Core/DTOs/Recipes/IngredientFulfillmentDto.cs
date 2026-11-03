namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class IngredientFulfillmentDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    /// <summary>The amount the recipe needs, in <see cref="QuantityUnitName"/>.</summary>
    public decimal RequiredAmount { get; set; }

    /// <summary>
    /// The stock left for this line, in <see cref="AvailableQuantityUnitName"/>. A product's lines take
    /// their share of its stock in recipe order, so a second line sees what the first left.
    /// </summary>
    public decimal AvailableAmount { get; set; }

    public bool IsSufficient { get; set; }
    public string? QuantityUnitName { get; set; }

    /// <summary>
    /// The unit <see cref="AvailableAmount"/> is in: the line's own unit when it converts to the product's
    /// stock unit, otherwise the stock unit.
    /// </summary>
    public string? AvailableQuantityUnitName { get; set; }

    /// <summary>
    /// False when the line's unit doesn't convert to the product's stock unit (an untagged unit, or volume
    /// against weight). <see cref="IsSufficient"/> then compares the two amounts as they are, as the stock
    /// check always did, and a client should show the line as not comparable rather than trust it.
    /// </summary>
    public bool CanCompare { get; set; } = true;
}
