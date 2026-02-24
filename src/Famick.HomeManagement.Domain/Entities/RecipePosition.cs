namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents an ingredient/position in a recipe step.
/// Links recipe steps to products with amounts and quantity units.
/// Example: "2 cups of flour", "3 eggs", "1 tsp vanilla extract"
/// </summary>
public class RecipePosition : BaseTenantEntity
{
    /// <summary>
    /// The recipe step this ingredient belongs to
    /// </summary>
    public Guid RecipeStepId { get; set; }

    /// <summary>
    /// Product used as ingredient
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Amount/quantity of the ingredient (must be > 0)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gram equivalent of the amount for nutritional calculations
    /// </summary>
    public decimal AmountInGrams { get; set; }

    /// <summary>
    /// Quantity unit for this ingredient (defaults to product's stock QU)
    /// </summary>
    public Guid? QuantityUnitId { get; set; }

    /// <summary>
    /// Optional note (preparation instructions, substitutions)
    /// Example: "finely chopped", "or use honey instead", "room temperature"
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Ingredient group for organizing recipe display
    /// Example: "Dry Ingredients", "Wet Ingredients", "Topping"
    /// </summary>
    public string? IngredientGroup { get; set; }

    /// <summary>
    /// Only check if at least one unit of this product is in stock
    /// (don't check for exact amount when checking recipe fulfillment)
    /// </summary>
    public bool OnlyCheckSingleUnitInStock { get; set; }

    /// <summary>
    /// Don't check stock fulfillment for this ingredient
    /// (useful for optional ingredients or those always assumed in stock like water, salt)
    /// </summary>
    public bool NotCheckStockFulfillment { get; set; }

    /// <summary>
    /// Display order for sorting ingredients within a step
    /// </summary>
    public int SortOrder { get; set; }

    // Navigation properties

    /// <summary>
    /// The recipe step this ingredient belongs to
    /// </summary>
    public virtual RecipeStep RecipeStep { get; set; } = null!;

    /// <summary>
    /// The product used as ingredient
    /// </summary>
    public virtual Product Product { get; set; } = null!;

    /// <summary>
    /// The quantity unit for this ingredient
    /// </summary>
    public virtual QuantityUnit? QuantityUnit { get; set; }
}
