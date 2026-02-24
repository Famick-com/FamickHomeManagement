using System.ComponentModel;

namespace Famick.HomeManagement.Mobile.Models;

#region Recipe Response DTOs

public class RecipeSummary : INotifyPropertyChanged
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Source { get; set; }
    public int Servings { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public int StepCount { get; set; }
    public int NestedRecipeCount { get; set; }
    public DateTime UpdatedAt { get; set; }

    private ImageSource? _thumbnailSource;

    /// <summary>
    /// Pre-loaded thumbnail for authenticated image URLs. Bind to this in XAML.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ImageSource? ThumbnailSource
    {
        get => _thumbnailSource;
        set
        {
            _thumbnailSource = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailSource)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class RecipeDetail
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Source { get; set; }
    public int Servings { get; set; }
    public string? Notes { get; set; }
    public string? Attribution { get; set; }
    public Guid? CreatedByContactId { get; set; }
    public string? CreatedByContactName { get; set; }
    public List<RecipeStep> Steps { get; set; } = new();
    public List<RecipeImage> Images { get; set; } = new();
    public List<NestedRecipe> NestedRecipes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? PrimaryImageUrl => Images.FirstOrDefault(i => i.IsPrimary)?.DisplayUrl
                                      ?? Images.FirstOrDefault()?.DisplayUrl;
}

public class RecipeStep
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int StepOrder { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImageExternalUrl { get; set; }
    public string? VideoUrl { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = new();

    public string DisplayImageUrl => !string.IsNullOrEmpty(ImageExternalUrl) ? ImageExternalUrl
        : !string.IsNullOrEmpty(ImageUrl) ? ImageUrl : string.Empty;
    public string DisplayTitle => !string.IsNullOrEmpty(Title) ? Title : $"Step {StepOrder}";

    /// <summary>
    /// Pre-loaded image source for authenticated image URLs. Set by mobile pages after downloading.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ImageSource? LoadedImageSource { get; set; }
}

public class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeStepId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Guid? QuantityUnitId { get; set; }
    public string? QuantityUnitName { get; set; }
    public string? Note { get; set; }
    public string? IngredientGroup { get; set; }
    public int SortOrder { get; set; }

    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (Amount > 0) parts.Add(Amount.ToString("G"));
            if (!string.IsNullOrEmpty(QuantityUnitName)) parts.Add(QuantityUnitName);
            parts.Add(ProductName);
            if (!string.IsNullOrEmpty(Note)) parts.Add($"({Note})");
            return string.Join(" ", parts);
        }
    }
}

public class RecipeImage
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public bool IsPrimary { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public string? ExternalThumbnailUrl { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public string DisplayUrl => !string.IsNullOrEmpty(ExternalUrl) ? ExternalUrl : Url;
    public string ThumbnailDisplayUrl => !string.IsNullOrEmpty(ExternalThumbnailUrl)
        ? ExternalThumbnailUrl : DisplayUrl;

    /// <summary>
    /// Pre-loaded image source for authenticated image URLs. Set by mobile pages after downloading.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ImageSource? LoadedImageSource { get; set; }
}

public class NestedRecipe
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
}

public class RecipeShareToken
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string ShareUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class RecipeFulfillment
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public bool CanBeMade { get; set; }
    public List<RecipeFulfillmentItem> Ingredients { get; set; } = new();
}

public class RecipeFulfillmentItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal RequiredAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public string? QuantityUnitName { get; set; }
    public bool IsSufficient { get; set; }
}

#endregion

#region Recipe Request DTOs

public class CreateRecipeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Source { get; set; }
    public int Servings { get; set; } = 1;
    public string? Notes { get; set; }
    public string? Attribution { get; set; }
}

public class UpdateRecipeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Source { get; set; }
    public int Servings { get; set; } = 1;
    public string? Notes { get; set; }
    public string? Attribution { get; set; }
}

public class CreateRecipeStepRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
}

public class UpdateRecipeStepRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
}

public class CreateRecipeIngredientRequest
{
    public Guid ProductId { get; set; }
    public decimal Amount { get; set; }
    public Guid? QuantityUnitId { get; set; }
    public string? Note { get; set; }
}

public class ReorderStepsRequest
{
    public List<Guid> StepIds { get; set; } = new();
}

public class AddToShoppingListRequest
{
    public Guid ShoppingListId { get; set; }
    public int? Servings { get; set; }
}

#endregion

#region Product Search DTOs (for ingredient autocomplete)

public class ProductSearchResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ProductGroupName { get; set; }
    public bool IsCreateOption => Id == Guid.Empty;
}

public class QuantityUnitSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

#endregion
