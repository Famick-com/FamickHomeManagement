using Famick.HomeManagement.Core.DTOs.Recipes;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Imports a recipe from a web page's schema.org Recipe data into the household.
/// </summary>
public interface IRecipeImportService
{
    /// <summary>
    /// Fetches the page, reads its recipe, matches each ingredient line to a product, and saves the recipe
    /// with its steps, ingredients, photo and credit to the source.
    /// </summary>
    /// <exception cref="Exceptions.RecipeImportException">The page couldn't be fetched or has no recipe.</exception>
    /// <exception cref="Exceptions.RecipeAlreadyImportedException">
    /// The household already has a recipe from this page, and <paramref name="allowDuplicate"/> is false.
    /// </exception>
    Task<RecipeImportResultDto> ImportAsync(string url, bool allowDuplicate, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Runs a queued import job and records how it ended on the job. Nothing is saved for a job that
    /// was cancelled before the recipe was.
    /// </summary>
    Task RunJobAsync(Guid jobId, CancellationToken ct = default);
}
