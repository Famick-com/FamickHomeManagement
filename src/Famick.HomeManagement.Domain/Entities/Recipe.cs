namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a recipe with step-based instructions and ingredients.
/// Recipes contain ordered steps, each with their own ingredients and instructions.
/// </summary>
public class Recipe : BaseTenantEntity
{
    /// <summary>
    /// Recipe name (e.g., "Chocolate Chip Cookies", "Spaghetti Carbonara")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Source URL or free text describing where the recipe came from
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Number of servings this recipe yields
    /// </summary>
    public int Servings { get; set; } = 1;

    /// <summary>
    /// Append-only notes field for additional context
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Copyright or attribution notice for the recipe source
    /// </summary>
    public string? Attribution { get; set; }

    /// <summary>
    /// Whether this recipe represents a full meal (for future meal planning)
    /// </summary>
    public bool IsMeal { get; set; }

    /// <summary>
    /// Optional link to the contact who created/submitted this recipe
    /// </summary>
    public Guid? CreatedByContactId { get; set; }

    // Navigation properties

    /// <summary>
    /// The contact who created this recipe
    /// </summary>
    public virtual Contact? CreatedByContact { get; set; }

    /// <summary>
    /// Ordered steps for this recipe
    /// </summary>
    public virtual ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();

    /// <summary>
    /// Images associated with this recipe
    /// </summary>
    public virtual ICollection<RecipeImage> Images { get; set; } = new List<RecipeImage>();

    /// <summary>
    /// Nested recipes that this recipe includes
    /// </summary>
    public virtual ICollection<RecipeNesting> NestedRecipes { get; set; } = new List<RecipeNesting>();

    /// <summary>
    /// Parent recipes that include this recipe
    /// </summary>
    public virtual ICollection<RecipeNesting> ParentRecipes { get; set; } = new List<RecipeNesting>();

    /// <summary>
    /// Share tokens for public sharing of this recipe
    /// </summary>
    public virtual ICollection<RecipeShareToken> ShareTokens { get; set; } = new List<RecipeShareToken>();
}
