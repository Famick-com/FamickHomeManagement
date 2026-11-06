using Famick.HomeManagement.Core.DTOs.Recipes;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Recipe imports run in the background, so a slow recipe site doesn't hold a request open past
/// what a client will wait, and the user can cancel one.
/// </summary>
public interface IRecipeImportJobService
{
    /// <summary>Starts importing the page and returns the running job.</summary>
    Task<RecipeImportJobDto> StartAsync(string url, bool allowDuplicate, Guid? userId, CancellationToken ct = default);

    /// <summary>The job as it stands, with the recipe once it has one; null when the household has no such job.</summary>
    Task<RecipeImportJobDto?> GetAsync(Guid jobId, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Cancels the job if it's still running, and returns it as it then stands. A job that already
    /// succeeded stays succeeded, with its recipe. Null when the household has no such job.
    /// </summary>
    Task<RecipeImportJobDto?> CancelAsync(Guid jobId, CancellationToken ct = default);
}
