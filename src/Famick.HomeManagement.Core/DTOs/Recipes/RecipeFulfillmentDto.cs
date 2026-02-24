namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class RecipeFulfillmentDto
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public bool CanBeMade { get; set; }
    public List<IngredientFulfillmentDto> Ingredients { get; set; } = new();
}
