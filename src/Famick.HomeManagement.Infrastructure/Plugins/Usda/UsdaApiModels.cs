using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Infrastructure.Plugins.Usda;

/// <summary>
/// USDA FoodData Central API models
/// </summary>

#region Search Models

/// <summary>
/// Search request for POST /foods/search
/// </summary>
public class UsdaSearchRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("dataType")]
    public List<string>? DataType { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; } = 1;

    [JsonPropertyName("sortBy")]
    public string? SortBy { get; set; }

    [JsonPropertyName("sortOrder")]
    public string? SortOrder { get; set; }

    [JsonPropertyName("brandOwner")]
    public string? BrandOwner { get; set; }
}

/// <summary>
/// Search response from POST /foods/search
/// </summary>
public class UsdaSearchResponse
{
    [JsonPropertyName("totalHits")]
    public int TotalHits { get; set; }

    [JsonPropertyName("currentPage")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("pageList")]
    public List<int>? PageList { get; set; }

    [JsonPropertyName("foods")]
    public List<UsdaSearchFood> Foods { get; set; } = new();
}

/// <summary>
/// Food item in search results
/// </summary>
public class UsdaSearchFood
{
    [JsonPropertyName("fdcId")]
    public int FdcId { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("dataType")]
    public string? DataType { get; set; }

    [JsonPropertyName("gtinUpc")]
    public string? GtinUpc { get; set; }

    [JsonPropertyName("brandOwner")]
    public string? BrandOwner { get; set; }

    [JsonPropertyName("brandName")]
    public string? BrandName { get; set; }

    [JsonPropertyName("foodCategory")]
    public string? FoodCategory { get; set; }

    [JsonPropertyName("ingredients")]
    public string? Ingredients { get; set; }

    [JsonPropertyName("servingSize")]
    public decimal? ServingSize { get; set; }

    [JsonPropertyName("servingSizeUnit")]
    public string? ServingSizeUnit { get; set; }

    [JsonPropertyName("householdServingFullText")]
    public string? HouseholdServingFullText { get; set; }

    [JsonPropertyName("foodNutrients")]
    public List<UsdaSearchNutrient>? FoodNutrients { get; set; }
}

/// <summary>
/// Nutrient in search results (abbreviated)
/// </summary>
public class UsdaSearchNutrient
{
    [JsonPropertyName("nutrientId")]
    public int NutrientId { get; set; }

    [JsonPropertyName("nutrientName")]
    public string? NutrientName { get; set; }

    [JsonPropertyName("nutrientNumber")]
    public string? NutrientNumber { get; set; }

    [JsonPropertyName("unitName")]
    public string? UnitName { get; set; }

    [JsonPropertyName("value")]
    public decimal? Value { get; set; }
}

#endregion

#region Food Detail Models

/// <summary>
/// Full food details from GET /food/{fdcId}
/// </summary>
public class UsdaFoodDetail
{
    [JsonPropertyName("fdcId")]
    public int FdcId { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("dataType")]
    public string? DataType { get; set; }

    [JsonPropertyName("gtinUpc")]
    public string? GtinUpc { get; set; }

    [JsonPropertyName("brandOwner")]
    public string? BrandOwner { get; set; }

    [JsonPropertyName("brandName")]
    public string? BrandName { get; set; }

    [JsonPropertyName("ingredients")]
    public string? Ingredients { get; set; }

    [JsonPropertyName("servingSize")]
    public decimal? ServingSize { get; set; }

    [JsonPropertyName("servingSizeUnit")]
    public string? ServingSizeUnit { get; set; }

    [JsonPropertyName("householdServingFullText")]
    public string? HouseholdServingFullText { get; set; }

    [JsonPropertyName("foodNutrients")]
    public List<UsdaFoodNutrient>? FoodNutrients { get; set; }

    [JsonPropertyName("labelNutrients")]
    public UsdaLabelNutrients? LabelNutrients { get; set; }
}

/// <summary>
/// Detailed nutrient information
/// </summary>
public class UsdaFoodNutrient
{
    [JsonPropertyName("nutrient")]
    public UsdaNutrient? Nutrient { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }
}

public class UsdaNutrient
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("unitName")]
    public string? UnitName { get; set; }
}

/// <summary>
/// Label nutrients (nutrition facts panel format)
/// </summary>
public class UsdaLabelNutrients
{
    [JsonPropertyName("fat")]
    public UsdaLabelNutrientValue? Fat { get; set; }

    [JsonPropertyName("saturatedFat")]
    public UsdaLabelNutrientValue? SaturatedFat { get; set; }

    [JsonPropertyName("transFat")]
    public UsdaLabelNutrientValue? TransFat { get; set; }

    [JsonPropertyName("cholesterol")]
    public UsdaLabelNutrientValue? Cholesterol { get; set; }

    [JsonPropertyName("sodium")]
    public UsdaLabelNutrientValue? Sodium { get; set; }

    [JsonPropertyName("carbohydrates")]
    public UsdaLabelNutrientValue? Carbohydrates { get; set; }

    [JsonPropertyName("fiber")]
    public UsdaLabelNutrientValue? Fiber { get; set; }

    [JsonPropertyName("sugars")]
    public UsdaLabelNutrientValue? Sugars { get; set; }

    [JsonPropertyName("protein")]
    public UsdaLabelNutrientValue? Protein { get; set; }

    [JsonPropertyName("calcium")]
    public UsdaLabelNutrientValue? Calcium { get; set; }

    [JsonPropertyName("iron")]
    public UsdaLabelNutrientValue? Iron { get; set; }

    [JsonPropertyName("potassium")]
    public UsdaLabelNutrientValue? Potassium { get; set; }

    [JsonPropertyName("calories")]
    public UsdaLabelNutrientValue? Calories { get; set; }
}

public class UsdaLabelNutrientValue
{
    [JsonPropertyName("value")]
    public decimal? Value { get; set; }
}

#endregion

#region Nutrient ID Constants

/// <summary>
/// USDA nutrient IDs for common nutrients
/// </summary>
public static class UsdaNutrientIds
{
    public const int Energy = 1008; // kcal
    public const int Protein = 1003;
    public const int TotalFat = 1004;
    public const int Carbohydrates = 1005;
    public const int Fiber = 1079;
    public const int TotalSugars = 2000;
    public const int AddedSugars = 1235;
    public const int Calcium = 1087;
    public const int Iron = 1089;
    public const int Magnesium = 1090;
    public const int Phosphorus = 1091;
    public const int Potassium = 1092;
    public const int Sodium = 1093;
    public const int Zinc = 1095;
    public const int VitaminA = 1106; // RAE
    public const int VitaminC = 1162;
    public const int VitaminD = 1114; // D2+D3
    public const int VitaminE = 1109;
    public const int VitaminK = 1185;
    public const int Thiamin = 1165;
    public const int Riboflavin = 1166;
    public const int Niacin = 1167;
    public const int VitaminB6 = 1175;
    public const int Folate = 1177;
    public const int VitaminB12 = 1178;
    public const int Cholesterol = 1253;
    public const int SaturatedFat = 1258;
    public const int TransFat = 1257;
}

#endregion
