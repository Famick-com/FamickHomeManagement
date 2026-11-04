namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class RecipeFilterRequest
{
    public string? SearchTerm { get; set; }
    public bool? IsMeal { get; set; }
    public string? SortBy { get; set; } = "Name"; // Name, CreatedAt, UpdatedAt, AverageRating, MyRating
    public bool Descending { get; set; } = false;

    /// <summary>Only recipes whose household average, as shown, is at least this many stars.</summary>
    public decimal? MinRating { get; set; }
}
