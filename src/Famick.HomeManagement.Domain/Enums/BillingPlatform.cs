namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// The platform through which a subscription was purchased.
/// In self-hosted mode, this enum exists but is unused (tenants have no subscription).
/// </summary>
public enum BillingPlatform
{
    /// <summary>
    /// Web subscription via Stripe Checkout
    /// </summary>
    Stripe = 0,

    /// <summary>
    /// iOS subscription via App Store (RevenueCat)
    /// </summary>
    AppStore = 1,

    /// <summary>
    /// Android subscription via Google Play (RevenueCat)
    /// </summary>
    GooglePlay = 2,

    /// <summary>
    /// A purchase made against RevenueCat's Test Store, which needs no real store
    /// behind it. Distinct from the real platforms on purpose: these are engineering
    /// artefacts, and a tenant carrying this has not paid anybody.
    /// </summary>
    Test = 3,

    /// <summary>
    /// RevenueCat reported a store we do not sell through — Amazon, Paddle, Roku, a
    /// promotional grant — or reported none at all. Also covers changes that no store
    /// was party to: the trial lifecycle, and tier edits made from the admin dashboard.
    /// </summary>
    /// <remarks>
    /// Recorded rather than guessed. These used to fall through to
    /// <see cref="Stripe"/>, which quietly attributed a purchase to a platform it did
    /// not come from; the field exists to say where the money came from, so saying
    /// "not known" is the only honest answer when it is not known.
    /// </remarks>
    Unknown = 4
}
