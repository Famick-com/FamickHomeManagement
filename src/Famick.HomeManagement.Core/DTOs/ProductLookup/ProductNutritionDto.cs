namespace Famick.HomeManagement.Core.DTOs.ProductLookup;

/// <summary>
/// DTO for product nutrition information
/// </summary>
public class ProductNutritionDto
{
    public Guid? Id { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>
    /// External ID from the data source (e.g., fdcId from USDA)
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Plugin ID that provided this data
    /// </summary>
    public string DataSource { get; set; } = string.Empty;

    // Serving information
    public decimal? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public string? ServingSizeDescription { get; set; }
    public decimal? ServingsPerContainer { get; set; }

    /// <summary>
    /// Calculated total weight in grams (ServingSize × ServingsPerContainer)
    /// Only valid when ServingUnit is "g" or "G"
    /// </summary>
    public decimal? TotalWeightGrams =>
        ServingSize.HasValue && ServingsPerContainer.HasValue &&
        (ServingUnit?.Equals("g", StringComparison.OrdinalIgnoreCase) == true ||
         ServingUnit?.Equals("G", StringComparison.OrdinalIgnoreCase) == true)
            ? ServingSize.Value * ServingsPerContainer.Value
            : null;

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

    // Metadata
    public string? BrandOwner { get; set; }
    public string? BrandName { get; set; }
    public string? Ingredients { get; set; }

    public DateTime? LastUpdatedFromSource { get; set; }
}
