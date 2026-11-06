using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// One import of a recipe from a web page, run in the background while the client checks on it.
/// </summary>
/// <remarks>
/// State lives here rather than in memory: the cloud runs more than one instance, so the request
/// that checks on an import, or cancels it, may not reach the one running it.
/// </remarks>
public class RecipeImportJob : BaseTenantEntity
{
    /// <summary>Who asked for it.</summary>
    public Guid? UserId { get; set; }

    public string Url { get; set; } = string.Empty;

    /// <summary>Whether to import the page again when the household already has a recipe from it.</summary>
    public bool AllowDuplicate { get; set; }

    public RecipeImportJobStatus Status { get; set; }

    /// <summary>
    /// Changed with every change of <see cref="Status"/>. The import saves its recipe and marks the job
    /// done only while this is the value it read, so a cancel that lands first leaves no recipe.
    /// </summary>
    public Guid Version { get; set; }

    /// <summary>The recipe the import made.</summary>
    public Guid? RecipeId { get; set; }

    /// <summary>How each ingredient line was matched, as JSON, for the client to report.</summary>
    public string? IngredientsJson { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>The recipe already imported from the page, when that's why it failed.</summary>
    public Guid? ExistingRecipeId { get; set; }

    public DateTime? CompletedAt { get; set; }
}
