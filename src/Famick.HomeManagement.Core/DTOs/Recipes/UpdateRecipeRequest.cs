namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>
/// Request to update an existing recipe.
/// </summary>
public class UpdateRecipeRequest
{
    /// <summary>
    /// Recipe name (required, max 200 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Source URL or free text describing where the recipe came from (max 2000 characters).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Number of servings this recipe yields (must be > 0).
    /// </summary>
    public int Servings { get; set; } = 1;

    /// <summary>
    /// Append-only notes field for additional context.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Copyright or attribution notice (max 1000 characters).
    /// </summary>
    public string? Attribution { get; set; }

    /// <summary>
    /// Whether this recipe represents a full meal.
    /// </summary>
    public bool IsMeal { get; set; }

    /// <summary>
    /// Optional contact ID for the recipe creator/source.
    /// </summary>
    public Guid? CreatedByContactId { get; set; }
}
