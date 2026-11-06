namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class IngredientRequirementDto
{
    /// <summary>
    /// Null for a line that isn't linked to a product. Such a line is never combined with another, and
    /// shows <see cref="OriginalText"/>.
    /// </summary>
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    /// <summary>The line as written in its source, kept for imported lines.</summary>
    public string? OriginalText { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? QuantityUnitId { get; set; }
    public string? QuantityUnitName { get; set; }
    public string? IngredientGroup { get; set; }

    /// <summary>
    /// The distinct notes of the combined ingredients, joined with "; ". Null when none has a note.
    /// </summary>
    public string? Note { get; set; }
}
