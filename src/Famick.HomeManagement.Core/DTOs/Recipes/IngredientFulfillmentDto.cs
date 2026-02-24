namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class IngredientFulfillmentDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal RequiredAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public bool IsSufficient { get; set; }
    public string? QuantityUnitName { get; set; }
}
