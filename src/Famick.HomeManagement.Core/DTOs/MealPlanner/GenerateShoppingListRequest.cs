namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class GenerateShoppingListRequest
{
    public Guid ShoppingListId { get; set; }
    public bool IncludeInStock { get; set; }
}
