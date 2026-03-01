namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class MealNutritionDto
{
    public Guid MealId { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProteinGrams { get; set; }
    public decimal TotalCarbsGrams { get; set; }
    public decimal TotalFatGrams { get; set; }
    public List<MealItemNutritionDto> ItemNutrition { get; set; } = new();
}

public class MealItemNutritionDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatGrams { get; set; }
    public bool HasNutritionData { get; set; }
}
