namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>
/// Request to import a recipe from a web page that publishes schema.org Recipe data.
/// </summary>
public class ImportRecipeRequest
{
    /// <summary>
    /// The recipe page's URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Import even when the household already has a recipe from this page.
    /// </summary>
    public bool AllowDuplicate { get; set; }
}
