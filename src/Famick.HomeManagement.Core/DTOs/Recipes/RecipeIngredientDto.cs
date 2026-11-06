namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class RecipeIngredientDto
{
    public Guid Id { get; set; }
    public Guid RecipeStepId { get; set; }
    /// <summary>Null for a line that isn't linked to a product; <see cref="OriginalText"/> then carries it.</summary>
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    /// <summary>The line as written in its source, kept for imported lines.</summary>
    public string? OriginalText { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountInGrams { get; set; }
    public Guid? QuantityUnitId { get; set; }
    public string? QuantityUnitName { get; set; }
    public string? Note { get; set; }
    public string? IngredientGroup { get; set; }
    public bool OnlyCheckSingleUnitInStock { get; set; }
    public bool NotCheckStockFulfillment { get; set; }
    public int SortOrder { get; set; }
}
