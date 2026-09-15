using System.Text.Json;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// What each plan is called, what it says, and which tier it is sold as — read from the
/// store offering's metadata.
/// </summary>
/// <remarks>
/// The store's own product model does not expose a title or description through the
/// binding we use: a product is a price, a duration and an identifier, nothing more. So
/// the display copy has to come from somewhere, and offering metadata is edited in the
/// RevenueCat dashboard, which means it can be corrected without shipping a build.
///
/// <para>The expected shape, keyed by product id:</para>
/// <code>
/// {
///   "famick_home_annual": {
///     "tier": "Home",
///     "title": "Home",
///     "description": "Everything in Organize, plus…"
///   }
/// }
/// </code>
///
/// <para><b>Nothing validates this against the server.</b> Entitlement is resolved from the
/// entitlements RevenueCat grants, read off the webhook as <c>entitlement_ids</c> — so the
/// tier named here is a second, independent statement about the same product. If the two
/// disagree, this screen is advertising something the household will not receive. Every
/// value read here is display copy and nothing more.</para>
///
/// <para>Deliberately free of MAUI and store-SDK types so it can be exercised by the shared
/// unit tests, which cannot reference the mobile project.</para>
/// </remarks>
public sealed class PlanCopy
{
    public sealed record Entry(SubscriptionTier? Tier, string? Title, string? Description);

    private readonly IReadOnlyDictionary<string, Entry> _entries;

    private PlanCopy(IReadOnlyDictionary<string, Entry> entries) => _entries = entries;

    /// <summary>Nothing configured. Every lookup misses, and callers fall back.</summary>
    public static PlanCopy Empty { get; } =
        new(new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase));

    /// <summary>The copy for a product id, or null when the metadata does not mention it.</summary>
    public Entry? For(string productId) =>
        _entries.TryGetValue(productId, out var entry) ? entry : null;

    /// <summary>
    /// Reads offering metadata, skipping anything malformed.
    /// </summary>
    /// <remarks>
    /// Never throws. Metadata is edited in a web console by hand, so a typo, a missing key
    /// or a value of the wrong shape is a question of when rather than whether — and none
    /// of those should take down a screen someone is trying to pay us on. A bad entry is
    /// dropped and logged; the plan still renders, labelled with its product id.
    /// </remarks>
    public static PlanCopy From(IReadOnlyDictionary<string, JsonElement>? metadata, ILogger? logger = null)
    {
        if (metadata is not { Count: > 0 }) return Empty;

        var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        foreach (var (productId, value) in metadata)
        {
            if (string.IsNullOrWhiteSpace(productId)) continue;

            if (value.ValueKind != JsonValueKind.Object)
            {
                logger?.LogWarning(
                    "Store metadata for {ProductId} is not an object — ignoring it", productId);
                continue;
            }

            try
            {
                entries[productId] = new Entry(
                    ReadTier(value, productId, logger),
                    ReadString(value, "title"),
                    ReadString(value, "description"));
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not read store metadata for {ProductId}", productId);
            }
        }

        return new PlanCopy(entries);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static SubscriptionTier? ReadTier(JsonElement element, string productId, ILogger? logger)
    {
        var raw = ReadString(element, "tier");

        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (Enum.TryParse<SubscriptionTier>(raw, ignoreCase: true, out var tier)) return tier;

        // A tier we cannot read is better than a tier we guess at: the plan renders
        // ungrouped rather than being filed under the wrong heading.
        logger?.LogWarning(
            "Store metadata for {ProductId} names an unknown tier {Tier}", productId, raw);

        return null;
    }
}
