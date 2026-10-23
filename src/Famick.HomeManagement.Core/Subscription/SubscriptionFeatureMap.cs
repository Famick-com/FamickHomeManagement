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
    public const string StorageBins = "storagebins";
    public const string MealPlanner = "mealplanner";
    public const string Analytics = "analytics";
    /// <summary>
    /// Downloading your own household's data.
    /// </summary>
    /// <remarks>
    /// Deliberately absent from <c>FeatureTiers</c>, so <see cref="GetRequiredTier"/> answers
    /// Free. California's CCPA and Washington's MHMDA both give residents a right to obtain their
    /// personal data in a portable form, and Famick is US-only, so both are in scope. Charging for
    /// the legal minimum is a poor position to argue from. The paid extras live under
    /// <see cref="ScheduledExport"/>.
    /// </remarks>
    public const string DataExport = "dataexport";

    /// <summary>
    /// Exports on a schedule, delivered somewhere — the part it is fair to charge for.
    /// </summary>
    public const string ScheduledExport = "scheduledexport";

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
        [StorageBins] = SubscriptionTier.Home,
        [MealPlanner] = SubscriptionTier.Home,

        // Pro tier ($16.99/mo)
        [Analytics] = SubscriptionTier.Pro,
        [ScheduledExport] = SubscriptionTier.Pro,
        [ApiAccess] = SubscriptionTier.Pro,

        // DataExport is not here on purpose — see the constant. Adding it would put a paywall in
        // front of a statutory access right.
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
        [StorageBins] = "Organize physical storage with labeled bins and photo tracking.",
        [MealPlanner] = "Plan weekly meals and generate shopping lists from recipes.",
        [Analytics] = "Advanced analytics and insights for your household data.",
        [DataExport] = "Download a copy of everything in your household. Available on every plan.",
        [ScheduledExport] = "Automatic exports on a schedule, delivered where you want them.",
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
    /// What the trial grants while it runs.
    /// </summary>
    public const SubscriptionTier TrialTier = SubscriptionTier.Home;

    /// <summary>
    /// The tier a household's access is judged by: the better of what it bought and what the
    /// trial grants, for as long as the trial runs.
    /// </summary>
    /// <remarks>
    /// This is the one definition of the rule. The server middleware, the web client and the
    /// mobile app all consult it, so they cannot disagree about what is unlocked. It used to be
    /// written in each of them, and each only promoted a <see cref="SubscriptionTier.Free"/>
    /// household, so buying Organize mid-trial took Home-level features away from the household
    /// that had just paid. Buying any tier during a trial never reduces access.
    /// </remarks>
    public static SubscriptionTier GetEffectiveTier(SubscriptionTier tier, bool isTrialActive)
    {
        return isTrialActive && tier < TrialTier ? TrialTier : tier;
    }

    /// <summary>
    /// The tier whose data stays readable once a household can no longer write. A Free household
    /// whose trial has ended keeps Home-level reads, because that is what it had; a paid household
    /// keeps its own tier. Whether it may write is a separate question, answered by trial and
    /// subscription dates, not by this method.
    /// </summary>
    public static SubscriptionTier GetReadableTier(SubscriptionTier tier, bool isTrialActive)
    {
        return tier == SubscriptionTier.Free ? TrialTier : GetEffectiveTier(tier, isTrialActive);
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
}
