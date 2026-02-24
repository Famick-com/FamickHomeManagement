namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a hierarchical relationship between recipes (recipe includes another recipe).
/// Enables complex recipes that use other recipes as components.
/// Example: "Wedding Cake" includes "Vanilla Buttercream Frosting" recipe
/// </summary>
public class RecipeNesting : BaseTenantEntity
{
    /// <summary>
    /// Parent recipe that includes another recipe
    /// </summary>
    public Guid RecipeId { get; set; }

    /// <summary>
    /// Child recipe that is included in the parent recipe
    /// </summary>
    public Guid IncludesRecipeId { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent recipe
    /// </summary>
    public virtual Recipe Recipe { get; set; } = null!;

    /// <summary>
    /// The included (child) recipe
    /// </summary>
    public virtual Recipe IncludedRecipe { get; set; } = null!;
}
