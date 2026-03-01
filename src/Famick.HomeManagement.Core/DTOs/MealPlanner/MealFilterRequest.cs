namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealFilterRequest
{
    public string? SearchTerm { get; set; }
    public bool? IsFavorite { get; set; }
    public string? SortBy { get; set; }
    public bool Descending { get; set; }
}
