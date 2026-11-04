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

    /// <summary>The household average to one decimal place, or null when nobody has rated.</summary>
    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }

    /// <summary>The caller's own rating, or null when they haven't rated.</summary>
    public decimal? MyRating { get; set; }
    public DateTime UpdatedAt { get; set; }
}
