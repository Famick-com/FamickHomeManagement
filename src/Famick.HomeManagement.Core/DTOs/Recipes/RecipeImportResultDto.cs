using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>
/// An imported recipe, and how each of its ingredient lines was matched to a product.
/// </summary>
public class RecipeImportResultDto
{
    public RecipeDto Recipe { get; set; } = null!;

    /// <summary>The ingredient lines in page order.</summary>
    public List<ImportedIngredientDto> Ingredients { get; set; } = new();
}

public class ImportedIngredientDto
{
    /// <summary>The line as written on the page.</summary>
    public string Text { get; set; } = string.Empty;

    public ImportedIngredientOutcome Outcome { get; set; }

    /// <summary>The product the line was linked to; null when it is unlinked.</summary>
    public string? ProductName { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportedIngredientOutcome
{
    /// <summary>Linked to a product the household already had.</summary>
    LinkedFromHousehold,

    /// <summary>Linked to a generic product from the catalog, which the import added to the household.</summary>
    AddedFromCatalog,

    /// <summary>No product matched; the line is kept as its text, for the user to link.</summary>
    Unlinked
}
