using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// The cloud's view of a household's subscription, from <c>api/subscription</c>.
/// </summary>
/// <remarks>
/// Cloud-only. The endpoint lives in the cloud app and does not exist on a self-hosted
/// server, where the request would 404 — callers check
/// <see cref="Services.ApiSettings.IsCloudServer"/> first.
///
/// <para>Only the fields the plans screen needs are mirrored here. The one that matters is
/// <see cref="Platform"/>: it is the only way the app can tell whether Apple, Google or the
/// web owns the billing relationship, and therefore whether "manage subscription" can do
/// anything at all.</para>
///
/// <para>The server serialises enums as numbers rather than names, which
/// <c>System.Text.Json</c> reads back into these types without help. Typing them as the
/// real enums rather than <c>int</c> keeps that decision in one place.</para>
/// </remarks>
public class SubscriptionInfoDto
{
    public SubscriptionTier Tier { get; set; }
    public bool IsTrialActive { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }

    /// <summary>
    /// Who is billing the household, or null when nothing is being billed.
    /// </summary>
    public BillingPlatform? Platform { get; set; }
}
