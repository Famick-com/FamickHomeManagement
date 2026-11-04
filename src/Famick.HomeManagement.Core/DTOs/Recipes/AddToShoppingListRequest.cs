namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>
/// Request to add missing recipe ingredients to a shopping list.
/// </summary>
public class AddToShoppingListRequest
{
    /// <summary>
    /// The shopping list to add items to (required).
    /// </summary>
    public Guid ShoppingListId { get; set; }

    /// <summary>
    /// Number of servings to calculate ingredient amounts for.
    /// When null, uses the recipe's default servings.
    /// </summary>
    public int? Servings { get; set; }

    /// <summary>
    /// Only these products, when set: after making a recipe, the ones it was short of. Null adds every
    /// missing ingredient.
    /// </summary>
    public List<Guid>? ProductIds { get; set; }
}
