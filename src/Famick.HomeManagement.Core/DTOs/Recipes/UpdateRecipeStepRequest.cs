namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>
/// Request to update a recipe step.
/// </summary>
public class UpdateRecipeStepRequest
{
    /// <summary>
    /// Optional title for the step (max 200 characters).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Optional short description of what the step accomplishes (max 2000 characters).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Cooking/preparation instructions for this step (optional, max 10000 characters).
    /// A JSON null is stored as empty, so it is not rejected as a missing required value.
    /// </summary>
    public string Instructions { get; set => field = value ?? string.Empty; } = string.Empty;

    /// <summary>
    /// External video URL for this step (e.g., YouTube timestamp link).
    /// </summary>
    public string? VideoUrl { get; set; }
}
