namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// FHM-68 — buying a cloud subscription from inside the app, through the
/// platform store rather than the web checkout.
/// </summary>
/// <remarks>
/// Wraps the RevenueCat binding so the rest of the app never sees its types,
/// which keeps the store SDK swappable and the Plans page testable.
///
/// <para><b>This service reports purchases. It does not decide entitlement.</b>
/// What a purchase is worth is resolved server-side, from the product id, when
/// RevenueCat's webhook reaches the cloud — see <c>BillingService</c> in the
/// cloud repo. A client that decided its own tier would duplicate that logic in
/// a second place, put a proprietary concern in this ELv2 repo, and let a
/// tampered client grant itself Home. So a successful purchase here means "the
/// store took the money"; the tier still arrives from the server, and
/// <see cref="SubscriptionStateService"/> stays the only thing that answers
/// what the household is entitled to.</para>
///
/// <para><b>Cloud households only.</b> A self-hosted server has no tiers and
/// nothing to sell — <see cref="ApiSettings.IsSelfHostedServer"/> covers proxied
/// households too, whose remote access is billed from the self-hosted server
/// itself. Callers must not present any of this when that is true.</para>
/// </remarks>
public interface IPurchaseService
{
    /// <summary>
    /// Whether in-app purchase can run at all — a store SDK is present, the
    /// platform supports it, and <see cref="InitializeAsync"/> has completed.
    /// False on a self-hosted or proxied server, and before initialization.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Points the store SDK at a household, and must be called before anything
    /// else here.
    /// </summary>
    /// <param name="tenantId">
    /// The household's tenant id. <b>This is the contract with the server and it
    /// is not negotiable.</b> RevenueCat sends whatever it knows as
    /// <c>app_user_id</c>, and <c>BillingService.ResolveTenantAsync</c> parses
    /// that as a GUID to find the tenant. Initialize before sign-in, or with a
    /// user id instead of a tenant id, and RevenueCat mints an anonymous
    /// identifier: the purchase then succeeds, the webhook fails to resolve a
    /// tenant, and the household is charged for nothing. The failure is silent
    /// on both sides, which is why this takes a <see cref="Guid"/> rather than a
    /// string — there is no value to pass here that is not a tenant id.
    /// </param>
    /// <remarks>
    /// Safe to call again when the signed-in household changes; switching
    /// accounts must re-point the SDK or purchases land against the previous
    /// tenant.
    /// </remarks>
    Task InitializeAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The plans the store will sell right now, as the store describes them.
    /// </summary>
    /// <remarks>
    /// Empty is a normal answer, not an error: the store returns nothing while
    /// the Paid Apps contract is inactive (FHM-65) — including in sandbox, which
    /// reads like a bug and is not one — and when no product is available in the
    /// user's storefront. Callers should say so rather than showing an empty
    /// screen.
    /// </remarks>
    Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Presents the platform's purchase sheet for a plan and waits for it.
    /// </summary>
    /// <param name="productId">
    /// <see cref="SubscriptionPlan.ProductId"/> from <see cref="GetPlansAsync"/>.
    /// </param>
    /// <remarks>
    /// The sheet is system UI; the app cannot style it and does not see payment
    /// details. Returning <see cref="PurchaseOutcome.Purchased"/> means the store
    /// completed the transaction, not that the household's tier has changed yet
    /// — that arrives asynchronously over the webhook.
    /// </remarks>
    Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-associates subscriptions already bought with this store account.
    /// </summary>
    /// <remarks>
    /// App Review requires a subscription app to offer this, and its absence is
    /// a routine rejection. It is also the real mechanism by which a second
    /// household member on a second device picks up a subscription someone else
    /// bought.
    /// </remarks>
    Task<PurchaseResult> RestoreAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A purchasable plan, described by the store rather than by us.
/// </summary>
/// <remarks>
/// Every display field comes from the store so the app shows the real localized
/// price the user will be charged, which Apple requires and which a hardcoded
/// price silently gets wrong on a price change or in another currency.
///
/// <para>There is deliberately no tier on this type. Mapping a product id to
/// Organize or Home is the server's job (<c>ResolveTierFromProductId</c>), and
/// re-deriving it here would be the same logic in two repos under two licences,
/// drifting apart. A Plans page that needs to group plans by tier should get
/// that mapping from the server rather than parsing the id.</para>
/// </remarks>
public sealed class SubscriptionPlan
{
    /// <summary>
    /// Store product identifier, e.g. <c>famick_home_monthly</c>. The value the
    /// server maps to a tier, and the handle passed back to
    /// <see cref="IPurchaseService.PurchaseAsync"/>.
    /// </summary>
    public required string ProductId { get; init; }

    /// <summary>Localized display name, as configured in the store.</summary>
    public required string Title { get; init; }

    /// <summary>Localized description, as configured in the store.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Localized price, already formatted for the user's storefront — show this
    /// string rather than composing one from a number and a currency symbol.
    /// </summary>
    public required string Price { get; init; }

    /// <summary>How long one billing period lasts.</summary>
    public BillingPeriod Period { get; init; }

    /// <summary>
    /// The introductory offer the store will apply, if this account is eligible
    /// and one is configured — already localized, e.g. "1 month free".
    /// </summary>
    /// <remarks>
    /// Null when no offer applies. Whether Famick configures introductory offers
    /// at all is FHM-66: the cloud trial is granted server-side at signup and is
    /// nothing to do with the store, so an offer here would be a second free
    /// period stacked on it.
    /// </remarks>
    public string? IntroductoryOffer { get; init; }
}

/// <summary>How often a plan bills.</summary>
public enum BillingPeriod
{
    Unknown = 0,
    Monthly = 1,
    Annual = 2,
}

/// <summary>What happened when the user was sent to the store.</summary>
public sealed class PurchaseResult
{
    public required PurchaseOutcome Outcome { get; init; }

    /// <summary>
    /// The product bought or restored, when there was one.
    /// </summary>
    public string? ProductId { get; init; }

    /// <summary>
    /// Something to show the user when <see cref="Outcome"/> is
    /// <see cref="PurchaseOutcome.Failed"/>. Never a raw SDK error.
    /// </summary>
    public string? Message { get; init; }

    public bool IsSuccess => Outcome is PurchaseOutcome.Purchased or PurchaseOutcome.Restored;
}

/// <summary>
/// Outcomes of a purchase or restore.
/// </summary>
/// <remarks>
/// Cancellation is separated from failure because it is the common case and
/// must not be reported as an error — a user who changes their mind at the
/// purchase sheet has not hit a problem and should not be told they have.
/// </remarks>
public enum PurchaseOutcome
{
    /// <summary>The store completed a new purchase.</summary>
    Purchased = 0,

    /// <summary>A prior purchase was found and re-associated.</summary>
    Restored = 1,

    /// <summary>The user dismissed the purchase sheet. Not an error.</summary>
    Cancelled = 2,

    /// <summary>Restore found nothing to restore. Not an error.</summary>
    NothingToRestore = 3,

    /// <summary>The store refused or the SDK failed. See <see cref="PurchaseResult.Message"/>.</summary>
    Failed = 4,
}
