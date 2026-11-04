namespace Famick.HomeManagement.Core.DTOs.Recipes;

/// <summary>Sets the caller's rating of a recipe: 1.0 to 5.0 stars, in half stars.</summary>
public class SetRecipeRatingRequest
{
    public decimal Rating { get; set; }
}

/// <summary>A recipe's ratings as the caller sees them, after setting or clearing their own.</summary>
public class RecipeRatingDto
{
    /// <summary>The household average to one decimal place, or null when nobody has rated.</summary>
    public decimal? AverageRating { get; set; }

    public int RatingCount { get; set; }

    /// <summary>The caller's own rating, or null when they haven't rated.</summary>
    public decimal? MyRating { get; set; }
}
