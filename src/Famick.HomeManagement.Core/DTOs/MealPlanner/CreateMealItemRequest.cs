using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class CreateMealItemRequest
{
    public MealItemType ItemType { get; set; }
    public Guid? RecipeId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal? ProductQuantity { get; set; }
    public Guid? ProductQuantityUnitId { get; set; }
    public string? FreetextDescription { get; set; }
    public int SortOrder { get; set; }
}
