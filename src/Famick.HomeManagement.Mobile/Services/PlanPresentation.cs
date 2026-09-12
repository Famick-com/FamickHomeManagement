using Famick.HomeManagement.Core.Subscription;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Turns what the store is selling into what the plans screen shows.
/// </summary>
/// <remarks>
/// Kept apart from the page, and free of MAUI and store-SDK types, so the shared unit tests
/// can reach it — the mobile project targets iOS and Android and cannot be referenced from
/// a plain test assembly, so anything worth asserting has to live somewhere linkable.
/// </remarks>
public static class PlanPresentation
{
    /// <summary>One tier, with whatever billing periods the store offers for it.</summary>
    public sealed class TierCard
    {
        /// <summary>Null when the store metadata did not say which tier this is.</summary>
        public SubscriptionTier? Tier { get; init; }

        public required string Title { get; init; }
        public string? Description { get; init; }
        public SubscriptionPlan? Monthly { get; init; }
        public SubscriptionPlan? Annual { get; init; }

        /// <summary>Plans whose billing period the store did not describe.</summary>
        public IReadOnlyList<SubscriptionPlan> Other { get; init; } = [];

        /// <summary>What this tier unlocks, for the "what you get" list.</summary>
        public IReadOnlyList<string> Features { get; init; } = [];
    }

    /// <summary>
    /// Groups plans into one card per tier, richest first.
    /// </summary>
    /// <remarks>
    /// Ordered to match the ranking configured in App Store Connect, where Home sits above
    /// Organize. Getting that backwards is not cosmetic — the store treats rank as the
    /// difference between an upgrade and a downgrade.
    ///
    /// <para>Plans the metadata does not assign a tier are kept rather than dropped, in
    /// their own cards at the end. Something purchasable that the screen refuses to show is
    /// worse than something shown without a heading.</para>
    /// </remarks>
    public static IReadOnlyList<TierCard> GroupIntoTierCards(IReadOnlyList<SubscriptionPlan>? plans)
    {
        if (plans is not { Count: > 0 }) return [];

        var known = plans
            .Where(p => p.AdvertisedTier.HasValue)
            .GroupBy(p => p.AdvertisedTier!.Value)
            .OrderByDescending(g => g.Key)
            .Select(g => BuildCard(g.Key, [.. g]))
            .ToList();

        var untiered = plans
            .Where(p => !p.AdvertisedTier.HasValue)
            .Select(p => BuildCard(null, [p]));

        return [.. known, .. untiered];
    }

    private static TierCard BuildCard(SubscriptionTier? tier, IReadOnlyList<SubscriptionPlan> plans)
    {
        // The store's own copy wins when it is there; otherwise fall back to the tier name,
        // and only then to whatever the plan called itself.
        var titled = plans.FirstOrDefault(p => !string.Equals(p.Title, p.ProductId, StringComparison.Ordinal));

        return new TierCard
        {
            Tier = tier,
            Title = titled?.Title ?? tier?.ToString() ?? plans[0].Title,
            Description = plans.Select(p => p.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)),
            Monthly = plans.FirstOrDefault(p => p.Period == BillingPeriod.Monthly),
            Annual = plans.FirstOrDefault(p => p.Period == BillingPeriod.Annual),
            Other = [.. plans.Where(p => p.Period == BillingPeriod.Unknown)],
            Features = tier.HasValue ? BuildFeatureBullets(tier.Value) : []
        };
    }

    /// <summary>
    /// What a tier unlocks, in the wording already used everywhere else.
    /// </summary>
    /// <remarks>
    /// Read from the shared feature map rather than written out here, so the screen cannot
    /// promise something the gate will refuse — the same map decides both.
    /// </remarks>
    public static IReadOnlyList<string> BuildFeatureBullets(SubscriptionTier tier) =>
        [.. SubscriptionFeatureMap.GetAllFeatures()
            .Where(f => f.Value <= tier && f.Value != SubscriptionTier.Free)
            .OrderBy(f => f.Value)
            .ThenBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => SubscriptionFeatureMap.GetFeatureDescription(f.Key))];

    /// <summary>
    /// Whether the server has caught up with a purchase yet.
    /// </summary>
    /// <remarks>
    /// A purchase does not change entitlement on its own: the store tells RevenueCat, which
    /// tells the cloud, which decides what the product is worth. So after buying, the app
    /// polls and compares — it never assumes.
    ///
    /// <para>Deliberately not "is the tier the one we were selling". Checking against an
    /// expected tier would mean deriving, on the client, what a product id is worth — which
    /// is the server's decision and the one thing this whole path is arranged to avoid. Any
    /// improvement counts, whatever it turns out to be.</para>
    ///
    /// <para>A rise in tier, or an expired household becoming current again — the latter
    /// matters because re-subscribing at the tier you already had moves nothing else.</para>
    /// </remarks>
    public static bool HasUpgraded(
        string? tierBefore,
        bool wasExpired,
        string? tierAfter,
        bool isExpired)
    {
        if (wasExpired && !isExpired) return true;

        if (!TryParseTier(tierBefore, out var before) || !TryParseTier(tierAfter, out var after))
        {
            // An unreadable tier on either side tells us nothing. Reporting success here
            // would end the poll and tell someone their plan is live when nobody knows.
            return false;
        }

        return after > before;
    }

    private static bool TryParseTier(string? value, out SubscriptionTier tier) =>
        Enum.TryParse(value, ignoreCase: true, out tier);
}
