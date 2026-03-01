using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a single item within a meal composition (recipe, product, or freetext).
/// </summary>
public class MealItem : BaseEntity
{
    public Guid MealId { get; set; }

    public MealItemType ItemType { get; set; }

    /// <summary>
    /// Reference to a recipe (when ItemType is Recipe).
    /// </summary>
    public Guid? RecipeId { get; set; }

    /// <summary>
    /// Reference to a product (when ItemType is Product).
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// Quantity of the product (when ItemType is Product).
    /// </summary>
    public decimal? ProductQuantity { get; set; }

    /// <summary>
    /// Quantity unit for the product (when ItemType is Product).
    /// </summary>
    public Guid? ProductQuantityUnitId { get; set; }

    /// <summary>
    /// Freetext description (when ItemType is Freetext).
    /// </summary>
    public string? FreetextDescription { get; set; }

    /// <summary>
    /// Display order within the meal.
    /// </summary>
    public int SortOrder { get; set; }

    // Navigation properties
    public virtual Meal Meal { get; set; } = null!;
    public virtual Recipe? Recipe { get; set; }
    public virtual Product? Product { get; set; }
    public virtual QuantityUnit? ProductQuantityUnit { get; set; }
}
