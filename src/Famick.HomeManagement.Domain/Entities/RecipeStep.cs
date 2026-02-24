namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a single step in a recipe. Each step has instructions and
/// its own set of ingredients (RecipePositions).
/// </summary>
public class RecipeStep : BaseTenantEntity
{
    /// <summary>
    /// The recipe this step belongs to
    /// </summary>
    public Guid RecipeId { get; set; }

    /// <summary>
    /// Display order of this step within the recipe (1-based)
    /// </summary>
    public int StepOrder { get; set; }

    /// <summary>
    /// Optional title for this step (e.g., "Prepare the dough", "Make the sauce")
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Optional short description of what this step accomplishes
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The cooking/preparation instructions for this step
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// Stored filename for the step image (unique, generated)
    /// </summary>
    public string? ImageFileName { get; set; }

    /// <summary>
    /// Original filename from the upload
    /// </summary>
    public string? ImageOriginalFileName { get; set; }

    /// <summary>
    /// MIME content type of the step image
    /// </summary>
    public string? ImageContentType { get; set; }

    /// <summary>
    /// File size of the step image in bytes
    /// </summary>
    public long? ImageFileSize { get; set; }

    /// <summary>
    /// External URL for the step image (used instead of local storage)
    /// </summary>
    public string? ImageExternalUrl { get; set; }

    /// <summary>
    /// External video URL for this step (e.g., YouTube timestamp link)
    /// </summary>
    public string? VideoUrl { get; set; }

    // Navigation properties

    /// <summary>
    /// The recipe this step belongs to
    /// </summary>
    public virtual Recipe Recipe { get; set; } = null!;

    /// <summary>
    /// Ingredients used in this step
    /// </summary>
    public virtual ICollection<RecipePosition> Ingredients { get; set; } = new List<RecipePosition>();
}
