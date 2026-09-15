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

    /// <summary>What a plan card should offer, given what the household already has.</summary>
    public enum PlanAction
    {
        /// <summary>Nothing held, or the plan is unrelated to what is held. Sell it.</summary>
        Purchase = 0,

        /// <summary>This is the plan they are on. Do not sell it to them again.</summary>
        Current = 1,

        /// <summary>Richer than what they hold.</summary>
        Upgrade = 2,

        /// <summary>Cheaper than what they hold.</summary>
        Downgrade = 3,
    }

    /// <summary>
    /// What to offer for one plan card.
    /// </summary>
    /// <remarks>
    /// Selling someone the plan they are already on is the complaint this answers: the
    /// store would happily take a second payment for it, and on some platforms it simply
    /// refuses mid-sheet, which reads as a broken app rather than a redundant purchase.
    ///
    /// <para>An expired household is offered everything again — the tier they used to hold
    /// tells you nothing about what they can buy now, and re-subscribing to the same plan
    /// is the most likely thing they want.</para>
    ///
    /// <para>A trial counts as holding nothing. The tier reads Free throughout, so every
    /// plan is a straight purchase.</para>
    /// </remarks>
    public static PlanAction ActionFor(SubscriptionTier? cardTier, string? currentTier, bool isExpired)
    {
        if (cardTier is null) return PlanAction.Purchase;
        if (!IsOnAPaidPlan(currentTier, isExpired)) return PlanAction.Purchase;
        if (!TryParseTier(currentTier, out var held)) return PlanAction.Purchase;

        if (cardTier.Value == held) return PlanAction.Current;

        return cardTier.Value > held ? PlanAction.Upgrade : PlanAction.Downgrade;
    }

    /// <summary>
    /// Whether the household is already on a paid plan that is currently good.
    /// </summary>
    /// <remarks>
    /// Used to tell a restore that has nothing left to do from one that is waiting on the
    /// server. Restoring on a household that already holds the entitlement changes
    /// nothing, so waiting for a change means waiting for something that will never
    /// arrive — a minute of "activating your subscription" ending in a timeout that reads
    /// as failure, for an operation that succeeded before it started.
    ///
    /// <para>A trial does not count. The tier is Free during one, and a restore then is
    /// expected to move the household onto a paid plan, which is a change worth waiting
    /// for.</para>
    /// </remarks>
    public static bool IsOnAPaidPlan(string? tier, bool isExpired)
    {
        if (isExpired) return false;

        return TryParseTier(tier, out var parsed) && parsed > SubscriptionTier.Free;
    }

    private static bool TryParseTier(string? value, out SubscriptionTier tier) =>
        Enum.TryParse(value, ignoreCase: true, out tier);
}
