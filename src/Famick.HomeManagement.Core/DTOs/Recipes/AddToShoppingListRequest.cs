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
    /// Amounts to add in place of what the recipe is missing, when set: after making it, what it was short
    /// of, as consuming it reported. Each is in its line's unit, or the stock unit when that is null, and is
    /// added in whole purchase units less what is already on the list. Null adds every missing ingredient.
    /// </summary>
    public List<ConsumeRecipeLine>? Shortfalls { get; set; }
}
