namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>
/// Request to update an ingredient in a recipe step.
/// </summary>
public class UpdateRecipeIngredientRequest
{
    /// <summary>
    /// Amount/quantity of the ingredient (must be > 0).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gram equivalent of the amount for nutritional calculations.
    /// </summary>
    public decimal AmountInGrams { get; set; }

    /// <summary>
    /// Quantity unit for this ingredient.
    /// </summary>
    public Guid? QuantityUnitId { get; set; }

    /// <summary>
    /// Optional note (preparation instructions, substitutions).
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Ingredient group for organizing display.
    /// </summary>
    public string? IngredientGroup { get; set; }

    /// <summary>
    /// Only check if at least one unit of this product is in stock.
    /// </summary>
    public bool OnlyCheckSingleUnitInStock { get; set; }

    /// <summary>
    /// Don't check stock fulfillment for this ingredient.
    /// </summary>
    public bool NotCheckStockFulfillment { get; set; }

    /// <summary>
    /// Display order within the step.
    /// </summary>
    public int SortOrder { get; set; }
}
