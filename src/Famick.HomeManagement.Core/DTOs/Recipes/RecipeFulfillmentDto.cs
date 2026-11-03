namespace Famick.HomeManagement.Core.DTOs.Recipes;

public class RecipeFulfillmentDto
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public bool CanBeMade { get; set; }

    /// <summary>Whether any ingredient's unit couldn't be compared with its stock; see <see cref="IngredientFulfillmentDto.CanCompare"/>.</summary>
    public bool HasUncomparableIngredients { get; set; }
    public List<IngredientFulfillmentDto> Ingredients { get; set; } = new();
}
