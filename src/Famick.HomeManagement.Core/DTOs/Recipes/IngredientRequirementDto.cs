namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class IngredientRequirementDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public Guid? QuantityUnitId { get; set; }
    public string? QuantityUnitName { get; set; }
    public string? IngredientGroup { get; set; }

    /// <summary>
    /// The distinct notes of the combined ingredients, joined with "; ". Null when none has a note.
    /// </summary>
    public string? Note { get; set; }
}
