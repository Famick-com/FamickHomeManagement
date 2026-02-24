namespace Famick.HomeManagement.Core.Interfaces.Plugins;

/// <summary>
/// Nutrition information from a product lookup
/// </summary>
public class ProductLookupNutrition
{
    public required string Source { get; set; }

    public string? ExternalSourceId {get; set;}

    public decimal? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public decimal? ServingsPerContainer { get; set; }

    // Macronutrients
    public decimal? Calories { get; set; }
    public decimal? TotalFat { get; set; }
    public decimal? SaturatedFat { get; set; }
    public decimal? TransFat { get; set; }
    public decimal? Cholesterol { get; set; }
    public decimal? Sodium { get; set; }
    public decimal? TotalCarbohydrates { get; set; }
    public decimal? DietaryFiber { get; set; }
    public decimal? TotalSugars { get; set; }
    public decimal? AddedSugars { get; set; }
    public decimal? Protein { get; set; }

    // Vitamins
    public decimal? VitaminA { get; set; }
    public decimal? VitaminC { get; set; }
    public decimal? VitaminD { get; set; }
    public decimal? VitaminE { get; set; }
    public decimal? VitaminK { get; set; }
    public decimal? Thiamin { get; set; }
    public decimal? Riboflavin { get; set; }
    public decimal? Niacin { get; set; }
    public decimal? VitaminB6 { get; set; }
    public decimal? Folate { get; set; }
    public decimal? VitaminB12 { get; set; }

    // Minerals
    public decimal? Calcium { get; set; }
    public decimal? Iron { get; set; }
    public decimal? Magnesium { get; set; }
    public decimal? Phosphorus { get; set; }
    public decimal? Potassium { get; set; }
    public decimal? Zinc { get; set; }
}
