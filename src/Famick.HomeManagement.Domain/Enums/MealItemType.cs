namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Defines the type of item within a meal composition.
/// </summary>
public enum MealItemType
{
    /// <summary>
    /// A recipe from the recipe system
    /// </summary>
    Recipe = 0,

    /// <summary>
    /// A standalone product from inventory
    /// </summary>
    Product = 1,

    /// <summary>
    /// A freetext description (e.g., "side salad")
    /// </summary>
    Freetext = 2
}
