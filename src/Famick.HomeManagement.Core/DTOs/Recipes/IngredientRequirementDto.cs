namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class IngredientRequirementDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public Guid? QuantityUnitId { get; set; }
    public string? QuantityUnitName { get; set; }
    public string? IngredientGroup { get; set; }
}
