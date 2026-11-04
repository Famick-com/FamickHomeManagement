namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>
/// Takes what a recipe used out of stock after it was made. The lines start as the recipe's stock check
/// at <see cref="Servings"/>; a client can drop a line, for a substitution, or change its amount.
/// </summary>
public class ConsumeRecipeRequest
{
    /// <summary>The servings made. The lines are already scaled to it.</summary>
    public int? Servings { get; set; }

    public List<ConsumeRecipeLine> Lines { get; set; } = new();
}

/// <summary>
/// One product's amount to consume. <see cref="QuantityUnitId"/> is the recipe line's unit, or null for
/// the product's stock unit, which is how a line whose unit doesn't convert is sent once corrected.
/// </summary>
public class ConsumeRecipeLine
{
    public Guid ProductId { get; set; }
    public decimal Amount { get; set; }
    public Guid? QuantityUnitId { get; set; }
}
