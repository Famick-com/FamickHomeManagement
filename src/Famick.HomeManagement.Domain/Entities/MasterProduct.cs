namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// A shared product in the global master catalog, accessible by all tenants.
/// Seeded from an embedded JSON resource; tenants can also contribute products.
/// Tenant products reference this via MasterProductId and can override fields locally.
/// </summary>
public class MasterProduct : BaseEntity
{
    // Identity
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Brand { get; set; }

    // Packaging & measurement
    public string? ContainerType { get; set; }
    public decimal? GramsPerTbsp { get; set; }
    public string? IconSvg { get; set; }
    public decimal? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public decimal? ServingsPerContainer { get; set; }

    // Defaults
    public int DefaultBestBeforeDays { get; set; }
    public bool TracksBestBeforeDate { get; set; } = true;

    // Onboarding metadata
    public bool IsStaple { get; set; }
    public int Popularity { get; set; } = 3;

    /// <summary>
    /// JSON array of lifestyle tag strings (e.g., ["baby"], ["pet"], ["household"]).
    /// Templates with no lifestyle tags are mainstream grocery items included for all households.
    /// </summary>
    public string LifestyleTags { get; set; } = "[]";

    /// <summary>
    /// JSON array of allergen flag strings matching AllergenType enum names
    /// (e.g., ["Milk"], ["Wheat", "Gluten"]).
    /// </summary>
    public string AllergenFlags { get; set; } = "[]";

    /// <summary>
    /// JSON array of dietary conflict flag strings matching DietaryPreference enum names
    /// (e.g., ["Vegan", "Vegetarian"] for meat products).
    /// </summary>
    public string DietaryConflictFlags { get; set; } = "[]";

    /// <summary>
    /// JSON array of cooking style tag strings matching CookingStyleInterest enum names
    /// (e.g., ["DairyAndEggs"], ["MeatAndSeafood"]).
    /// </summary>
    public string CookingStyleTags { get; set; } = "[]";

    /// <summary>
    /// Hint for default Location assignment during product creation
    /// (e.g., "Pantry", "Refrigerator", "Freezer").
    /// </summary>
    public string? DefaultLocationHint { get; set; }

    /// <summary>
    /// Hint for default QuantityUnit assignment during product creation
    /// (e.g., "Piece", "Pound", "Gallon").
    /// </summary>
    public string? DefaultQuantityUnitHint { get; set; }

    /// <summary>
    /// Markdown-formatted attribution text for external data sources.
    /// </summary>
    public string? DataSourceAttribution { get; set; }

    /// <summary>
    /// Self-referencing parent for generic product hierarchy
    /// (e.g., "Milk" → "Whole Milk", "2% Milk").
    /// </summary>
    public Guid? ParentMasterProductId { get; set; }

    // Navigation properties
    public MasterProduct? ParentMasterProduct { get; set; }
    public ICollection<MasterProduct> ChildMasterProducts { get; set; } = new List<MasterProduct>();
    public ICollection<MasterProductBarcode> Barcodes { get; set; } = new List<MasterProductBarcode>();
    public MasterProductNutrition? Nutrition { get; set; }
    public ICollection<MasterProductImage> Images { get; set; } = new List<MasterProductImage>();
}
