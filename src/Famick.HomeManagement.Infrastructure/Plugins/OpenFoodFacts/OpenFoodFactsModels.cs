using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Infrastructure.Plugins.OpenFoodFacts;

/// <summary>
/// Response from Open Food Facts product lookup by barcode
/// GET /api/v2/product/{barcode}.json
/// </summary>
public class OpenFoodFactsProductResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("status_verbose")]
    public string? StatusVerbose { get; set; }

    [JsonPropertyName("product")]
    public OpenFoodFactsProduct? Product { get; set; }
}

/// <summary>
/// Response from Open Food Facts search endpoint
/// GET /cgi/search.pl?search_terms={query}&json=1
/// </summary>
public class OpenFoodFactsSearchResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("products")]
    public List<OpenFoodFactsProduct>? Products { get; set; }
}

/// <summary>
/// Product data from Open Food Facts API
/// </summary>
public class OpenFoodFactsProduct
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("product_name_en")]
    public string? ProductNameEn { get; set; }

    [JsonPropertyName("brands")]
    public string? Brands { get; set; }

    [JsonPropertyName("brands_tags")]
    public List<string>? BrandsTags { get; set; }

    [JsonPropertyName("categories")]
    public string? Categories { get; set; }

    [JsonPropertyName("categories_tags")]
    public List<string>? CategoriesTags { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("image_front_url")]
    public string? ImageFrontUrl { get; set; }

    [JsonPropertyName("image_front_small_url")]
    public string? ImageFrontSmallUrl { get; set; }

    [JsonPropertyName("image_front_thumb_url")]
    public string? ImageFrontThumbUrl { get; set; }

    [JsonPropertyName("image_small_url")]
    public string? ImageSmallUrl { get; set; }

    [JsonPropertyName("image_thumb_url")]
    public string? ImageThumbUrl { get; set; }

    [JsonPropertyName("ingredients_text")]
    public string? IngredientsText { get; set; }

    [JsonPropertyName("ingredients_text_en")]
    public string? IngredientsTextEn { get; set; }

    [JsonPropertyName("serving_size")]
    public string? ServingSize { get; set; }

    [JsonPropertyName("serving_quantity")]
    public decimal? ServingQuantity { get; set; }

    [JsonPropertyName("nutriscore_grade")]
    public string? NutriscoreGrade { get; set; }

    [JsonPropertyName("nova_group")]
    public int? NovaGroup { get; set; }

    [JsonPropertyName("nutriments")]
    public OpenFoodFactsNutriments? Nutriments { get; set; }
}

/// <summary>
/// Nutrition data from Open Food Facts
/// All values are per 100g unless noted otherwise
/// </summary>
public class OpenFoodFactsNutriments
{
    [JsonPropertyName("energy-kcal_serving")]
    public decimal? EnergyKcalServing { get; set; }

    [JsonPropertyName("energy-kcal_100g")]
    public decimal? EnergyKcal100g { get; set; }

    [JsonPropertyName("proteins_serving")]
    public decimal? ProteinsServing { get; set; }

    [JsonPropertyName("proteins_100g")]
    public decimal? Proteins100g { get; set; }

    [JsonPropertyName("fat_serving")]
    public decimal? FatServing { get; set; }

    [JsonPropertyName("fat_100g")]
    public decimal? Fat100g { get; set; }

    [JsonPropertyName("saturated-fat_serving")]
    public decimal? SaturatedFatServing { get; set; }

    [JsonPropertyName("saturated-fat_100g")]
    public decimal? SaturatedFat100g { get; set; }

    [JsonPropertyName("carbohydrates_serving")]
    public decimal? CarbohydratesServing { get; set; }

    [JsonPropertyName("carbohydrates_100g")]
    public decimal? Carbohydrates100g { get; set; }

    [JsonPropertyName("sugars_serving")]
    public decimal? SugarsServing { get; set; }

    [JsonPropertyName("sugars_100g")]
    public decimal? Sugars100g { get; set; }

    [JsonPropertyName("fiber_serving")]
    public decimal? FiberServing { get; set; }

    [JsonPropertyName("fiber_100g")]
    public decimal? Fiber100g { get; set; }

    [JsonPropertyName("salt_serving")]
    public decimal? SaltServing { get; set; }

    [JsonPropertyName("salt_100g")]
    public decimal? Salt100g { get; set; }

    [JsonPropertyName("sodium_serving")]
    public decimal? SodiumServing { get; set; }

    [JsonPropertyName("sodium_100g")]
    public decimal? Sodium100g { get; set; }
}
