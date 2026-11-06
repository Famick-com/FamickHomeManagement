using System.Text.Json.Serialization;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>
/// A recipe import running in the background: started by a POST, checked at
/// <c>api/v1/recipes/import/{jobId}</c>, and cancelled with a DELETE there.
/// </summary>
public class RecipeImportJobDto
{
    public Guid JobId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<RecipeImportJobStatus>))]
    public RecipeImportJobStatus Status { get; set; }

    /// <summary>The recipe and its lines' outcomes, once the import has succeeded.</summary>
    public RecipeImportResultDto? Result { get; set; }

    /// <summary>Why it failed: one of the <see cref="Exceptions.RecipeImportException"/> codes.</summary>
    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>The recipe already imported from the page, when that's why it failed.</summary>
    public Guid? ExistingRecipeId { get; set; }
}
