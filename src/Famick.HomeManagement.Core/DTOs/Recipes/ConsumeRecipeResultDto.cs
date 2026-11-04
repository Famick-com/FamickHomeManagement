namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>What making a recipe took out of stock, one entry per requested line, in request order.</summary>
public class ConsumeRecipeResultDto
{
    public List<ConsumedIngredientDto> Ingredients { get; set; } = new();
}

/// <summary>
/// One line's outcome, in <see cref="QuantityUnitName"/>. A line with too little stock consumes what there
/// was, and <see cref="MissingAmount"/> is what it was short by.
/// </summary>
public class ConsumedIngredientDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? QuantityUnitId { get; set; }
    public string? QuantityUnitName { get; set; }
    public decimal Amount { get; set; }
    public decimal ConsumedAmount { get; set; }
    public decimal MissingAmount { get; set; }
}
