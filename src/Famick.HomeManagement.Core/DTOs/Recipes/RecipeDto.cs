namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class RecipeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Source { get; set; }
    public int Servings { get; set; }
    public string? Notes { get; set; }
    public string? Attribution { get; set; }
    public bool IsMeal { get; set; }
    public Guid? CreatedByContactId { get; set; }
    public string? CreatedByContactName { get; set; }
    public List<RecipeStepDto> Steps { get; set; } = new();
    public List<RecipeImageDto> Images { get; set; } = new();
    public List<NestedRecipeDto> NestedRecipes { get; set; } = new();
    public List<RecipeShareDto> ShareTokens { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
