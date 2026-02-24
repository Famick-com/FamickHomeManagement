namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class RecipeStepDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int StepOrder { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string? ImageFileName { get; set; }
    public string? ImageOriginalFileName { get; set; }
    public string? ImageContentType { get; set; }
    public long? ImageFileSize { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageExternalUrl { get; set; }
    public string? VideoUrl { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
}
