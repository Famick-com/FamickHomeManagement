using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Subscription;

/// <summary>
/// Single source of truth for feature-area → subscription tier mapping.
/// Used by server middleware, web UI, and mobile app for consistent feature gating.
/// </summary>
public static class SubscriptionFeatureMap
{
    // Feature area constants
    public const string Contacts = "contacts";
    public const string Chores = "chores";
    public const string Equipment = "equipment";
    public const string Todos = "todos";
    public const string Shopping = "shopping";
    public const string Inventory = "inventory";
    public const string Products = "products";
    public const string Recipes = "recipes";
    public const string Vehicles = "vehicles";
    public const string StorageBins = "storagebins";
    public const string MealPlanner = "mealplanner";
    public const string Analytics = "analytics";
    public const string DataExport = "dataexport";
    public const string ApiAccess = "apiaccess";

    private static readonly Dictionary<string, SubscriptionTier> FeatureTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        // Organize tier ($3.99/mo)
        [Contacts] = SubscriptionTier.Organize,
        [Chores] = SubscriptionTier.Organize,
        [Equipment] = SubscriptionTier.Organize,
        [Todos] = SubscriptionTier.Organize,

        // Home tier ($8.99/mo)
        [Shopping] = SubscriptionTier.Home,
        [Inventory] = SubscriptionTier.Home,
        [Products] = SubscriptionTier.Home,
        [Recipes] = SubscriptionTier.Home,
        [Vehicles] = SubscriptionTier.Home,
        [StorageBins] = SubscriptionTier.Home,
        [MealPlanner] = SubscriptionTier.Home,

        // Pro tier ($16.99/mo)
        [Analytics] = SubscriptionTier.Pro,
        [DataExport] = SubscriptionTier.Pro,
        [ApiAccess] = SubscriptionTier.Pro,
    };

    private static readonly Dictionary<string, string> FeatureDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        [Contacts] = "Manage contacts, groups, and relationships for your household.",
        [Chores] = "Track and assign recurring household chores.",
        [Equipment] = "Manage home equipment, maintenance records, and documents.",
        [Todos] = "Create and manage household to-do lists.",
        [Shopping] = "Create shopping lists with store integration and barcode scanning.",
        [Inventory] = "Track your home inventory with expiration dates and stock levels.",
        [Products] = "Manage your product catalog with nutrition and barcode data.",
        [Recipes] = "Store and organize recipes with ingredient tracking.",
        [Vehicles] = "Track vehicle maintenance, mileage, and service schedules.",
        [StorageBins] = "Organize physical storage with labeled bins and photo tracking.",
        [MealPlanner] = "Plan weekly meals and generate shopping lists from recipes.",
        [Analytics] = "Advanced analytics and insights for your household data.",
        [DataExport] = "Export your household data in standard formats.",
        [ApiAccess] = "Programmatic API access for custom integrations.",
    };

    /// <summary>
    /// Gets the minimum tier required for a feature area.
    /// Returns Free if the feature is not gated.
    /// </summary>
    public static SubscriptionTier GetRequiredTier(string featureArea)
    {
        return FeatureTiers.TryGetValue(featureArea, out var tier) ? tier : SubscriptionTier.Free;
    }

    /// <summary>
    /// Checks if a feature is available at the given subscription tier.
    /// </summary>
    public static bool IsFeatureAvailable(string featureArea, SubscriptionTier currentTier)
    {
        return currentTier >= GetRequiredTier(featureArea);
    }

    /// <summary>
    /// Gets a short marketing description for the upgrade banner.
    /// </summary>
    public static string GetFeatureDescription(string featureArea)
    {
        return FeatureDescriptions.TryGetValue(featureArea, out var desc) ? desc : string.Empty;
    }

    /// <summary>
    /// Gets all defined feature areas with their required tiers.
    /// </summary>
    public static IReadOnlyDictionary<string, SubscriptionTier> GetAllFeatures()
    {
        return FeatureTiers;
    }

    /// <summary>
    /// Maps an API route prefix to a feature area.
    /// Used by server middleware to determine which feature a route belongs to.
    /// </summary>
    public static string? GetFeatureAreaForRoute(string routePrefix)
    {
        return routePrefix.ToLowerInvariant() switch
        {
            "/api/v1/contacts" => Contacts,
            "/api/v1/chores" => Chores,
            "/api/v1/equipment" => Equipment,
            "/api/v1/todoitems" => Todos,
            "/api/v1/shoppinglists" or "/api/v1/shoppinglocations" => Shopping,
            "/api/v1/stock" or "/api/v1/locations" or "/api/v1/quantity-units" => Inventory,
            "/api/v1/products" or "/api/v1/productgroups" or "/api/v1/product-lookup" or "/api/v1/storeintegrations" => Products,
            "/api/v1/recipes" => Recipes,
            "/api/v1/vehicles" => Vehicles,
            "/api/v1/storage-bins" => StorageBins,
            _ => null,
        };
    }
}
