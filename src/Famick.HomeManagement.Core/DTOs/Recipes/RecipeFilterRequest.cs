namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class RecipeFilterRequest
{
    public string? SearchTerm { get; set; }
    public bool? IsMeal { get; set; }
    public string? SortBy { get; set; } = "Name"; // Name, CreatedAt, UpdatedAt
    public bool Descending { get; set; } = false;
}
