namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class RecipeSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Source { get; set; }
    public int Servings { get; set; }
    public bool IsMeal { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public int StepCount { get; set; }
    public int NestedRecipeCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
