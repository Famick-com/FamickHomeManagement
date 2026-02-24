namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Stores nutrition information for a product, typically sourced from external databases (USDA, etc.)
/// One-to-one relationship with Product
/// </summary>
public class ProductNutrition : BaseTenantEntity
{
    public Guid ProductId { get; set; }

    /// <summary>
    /// External identifier from the data source (e.g., fdcId from USDA)
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Plugin ID that provided this data (e.g., "usda", "openfoodfacts")
    /// </summary>
    public string DataSource { get; set; } = string.Empty;

    // Serving information
    public decimal? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public decimal? ServingsPerContainer { get; set; }

    // Macronutrients (per serving)
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

    // Vitamins and Minerals
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
    public decimal? Calcium { get; set; }
    public decimal? Iron { get; set; }
    public decimal? Magnesium { get; set; }
    public decimal? Phosphorus { get; set; }
    public decimal? Potassium { get; set; }
    public decimal? Zinc { get; set; }

    // Metadata from source
    public string? BrandOwner { get; set; }
    public string? BrandName { get; set; }
    public string? Ingredients { get; set; }
    public string? ServingSizeDescription { get; set; }

    /// <summary>
    /// When the data was last fetched/updated from the external source
    /// </summary>
    public DateTime LastUpdatedFromSource { get; set; }

    // Navigation property
    public Product Product { get; set; } = null!;
}
