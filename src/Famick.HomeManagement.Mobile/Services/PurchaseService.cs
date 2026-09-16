using System.Text.Json;
using Famick.HomeManagement.Domain.Enums;
using Maui.RevenueCat.InAppBilling.Enums;
using Maui.RevenueCat.InAppBilling.Extensions;
using Maui.RevenueCat.InAppBilling.Models;
using Maui.RevenueCat.InAppBilling.Services;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// <see cref="IPurchaseService"/> over the RevenueCat binding.
/// </summary>
/// <remarks>
/// One implementation for both platforms rather than the <c>Platforms/iOS</c> +
/// <c>Platforms/Android</c> split the other platform services use, because the binding is
/// genuinely cross-platform — the only thing that differs is which API key to hand it, and
/// that is one expression.
///
/// <para>Nothing outside this file sees a RevenueCat type. That is the point of the
/// interface: the store SDK is a one-maintainer dependency sitting in the payment path, and
/// keeping it behind a boundary is what makes it replaceable.</para>
/// </remarks>
public class PurchaseService : IPurchaseService
{
    private readonly IRevenueCatBilling _billing;
    private readonly ApiSettings _apiSettings;
    private readonly ILogger<PurchaseService> _logger;

    /// <summary>Guards <see cref="InitializeAsync"/> against concurrent callers.</summary>
    /// <remarks>
    /// App start and a sign-in can both reach it, and they can race: two threads both see
    /// "not initialized", both call Initialize, and the second overwrites the first.
    /// </remarks>
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public PurchaseService(
        IRevenueCatBilling billing,
        ApiSettings apiSettings,
        ILogger<PurchaseService> logger)
    {
        _billing = billing;
        _apiSettings = apiSettings;
        _logger = logger;
    }

    /// <summary>
    /// The public SDK key for the store this build talks to.
    /// </summary>
    /// <remarks>
    /// Chosen at runtime rather than with <c>#if</c>, so one implementation serves both
    /// platforms. The cost is that each binary carries both keys — acceptable, because
    /// these are RevenueCat's <em>public</em> SDK keys, which are designed to ship inside
    /// app binaries. They are not secrets in the way the Syncfusion licence is.
    ///
    /// <para>Empty when the build environment did not supply one, which is the normal
    /// state of a clone with no secrets. <see cref="IsAvailable"/> checks for that so the
    /// app still runs; the purchase surface is simply absent.</para>
    /// </remarks>
    private static string ApiKey =>
        DeviceInfo.Current.Platform == Microsoft.Maui.Devices.DevicePlatform.iOS
        || DeviceInfo.Current.Platform == Microsoft.Maui.Devices.DevicePlatform.MacCatalyst
            ? LicenseKeys.RevenueCatIos
            : LicenseKeys.RevenueCatAndroid;

    /// <summary>
    /// The household the SDK is currently bound to, or null if binding did not succeed.
    /// </summary>
    /// <remarks>
    /// Being initialised is not the same as being bound to the right household. A failed
    /// re-point leaves the SDK initialised and still pointed at the previous tenant, and
    /// without this a purchase would then be credited to them.
    /// </remarks>
    private volatile string? _boundAppUserId;

    public bool IsAvailable =>
        _apiSettings.IsCloudServer()
        && !string.IsNullOrEmpty(ApiKey)
        && _billing.IsInitialized()
        // Initialised is not enough — it has to have bound to a household.
        && _boundAppUserId is not null;

    public async Task InitializeAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!_apiSettings.IsCloudServer())
        {
            // Self-hosted and proxied households have no tiers and nothing to sell. Do not
            // even point the SDK at them.
            return;
        }

        if (string.IsNullOrEmpty(ApiKey))
        {
            _logger.LogInformation("No RevenueCat key in this build — in-app purchase is unavailable");
            return;
        }

        var appUserId = tenantId.ToString();

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (!_billing.IsInitialized())
            {
                // The two-argument overload. The one-argument one mints an anonymous
                // identifier, which the cloud cannot parse as a tenant id — the purchase
                // then succeeds and the household is charged for nothing, silently on both
                // sides. There is no value to pass here that is not a tenant id.
                _billing.Initialize(ApiKey, appUserId);
                _boundAppUserId = appUserId;
                _logger.LogInformation("RevenueCat initialized for tenant {TenantId}", tenantId);
                return;
            }

            if (string.Equals(_billing.GetAppUserId(), appUserId, StringComparison.Ordinal))
            {
                _boundAppUserId = appUserId;
                return;
            }

            // Already pointed at a different household — someone switched accounts. Re-point
            // it, or their purchases land against the previous tenant.
            var result = await _billing.Login(appUserId, cancellationToken);

            if (result.IsError)
            {
                // Still initialised, still pointed at the previous household. Clearing the
                // binding makes IsAvailable false, so nothing can be bought until a
                // re-point succeeds — otherwise the purchase is credited to whoever was
                // signed in before.
                _boundAppUserId = null;

                _logger.LogError(
                    "Could not re-point RevenueCat to tenant {TenantId}, store access disabled: {Error}",
                    tenantId, result.Error);
                return;
            }

            _boundAppUserId = appUserId;
            _logger.LogInformation("RevenueCat re-pointed to tenant {TenantId}", tenantId);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return [];

        var offerings = await _billing.GetOfferings(false, cancellationToken);

        if (offerings.IsError || offerings.Value is null)
        {
            // Empty is a normal answer here, not a failure to report. The store returns
            // nothing while the Paid Apps contract is inactive — sandbox included — and
            // when nothing is sold in the user's storefront.
            _logger.LogWarning("Could not read store offerings: {Error}", offerings.Error);
            return [];
        }

        var current = offerings.Value.GetCurrent();

        if (current?.AvailablePackages is not { Count: > 0 })
        {
            // Name the offering: "no packages" and "no current offering at all" are
            // different dashboard mistakes that otherwise look identical from here.
            _logger.LogWarning(
                "Offering {Offering} has no purchasable packages ({Count} offerings returned)",
                current?.Identifier ?? "(none current)", offerings.Value.Count);

            return [];
        }

        _logger.LogInformation(
            "Offering {Offering} returned {Count} packages",
            current.Identifier, current.AvailablePackages.Count);

        var copy = PlanCopy.From(current.Metadata, _logger);

        return current.AvailablePackages
            .Where(p => p.Product is not null)
            .Select(p => ToPlan(p, copy))
            .ToList();
    }

    public async Task<PurchaseResult> PurchaseAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return Failure("Purchases aren't available on this device right now.");
        }

        var offerings = await _billing.GetOfferings(false, cancellationToken);
        var package = offerings.Value?.GetCurrent()?.AvailablePackages
            ?.FirstOrDefault(p => string.Equals(p.Product?.Sku, productId, StringComparison.Ordinal));

        if (package is null)
        {
            _logger.LogWarning("Asked to buy {ProductId}, which the store is not offering", productId);
            return Failure("That plan isn't available right now. Try again in a few minutes.");
        }

        var result = await _billing.PurchaseProduct(package, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Store completed a purchase of {ProductId}", productId);

            return new PurchaseResult
            {
                Outcome = PurchaseOutcome.Purchased,
                ProductId = result.Transaction?.ProductIdentifier ?? productId
            };
        }

        // Always say what the store reported, whatever we go on to make of it. Without this
        // the quiet outcomes are indistinguishable from the app doing nothing at all —
        // which is exactly how a simulated Test Store failure reads, since it arrives as a
        // cancellation and cancellations are deliberately silent in the UI.
        _logger.LogInformation(
            "Store reported {Status} for {ProductId}", result.Error, productId);

        // Not every error is a failure. Cancellation and pending approval both arrive here.
        return result.Error switch
        {
            PurchaseErrorStatus.PurchaseCancelledError =>
                new PurchaseResult { Outcome = PurchaseOutcome.Cancelled },

            // Ask to Buy, or a bank still checking the card. No money has moved and nothing
            // has gone wrong; the entitlement may follow much later, or never.
            PurchaseErrorStatus.PaymentPendingError =>
                new PurchaseResult
                {
                    Outcome = PurchaseOutcome.Pending,
                    ProductId = result.Transaction?.ProductIdentifier ?? productId
                },

            // They already own it — the store declining to charge twice. Treat it as the
            // restore it effectively is, so the screen refreshes instead of erroring.
            PurchaseErrorStatus.ProductAlreadyPurchasedError =>
                new PurchaseResult { Outcome = PurchaseOutcome.Restored, ProductId = productId },

            _ => Failed(result.Error, result.ErrorException, productId)
        };
    }

    public async Task<PurchaseResult> RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return Failure("Purchases aren't available on this device right now.");
        }

        var result = await _billing.RestoreTransactions(cancellationToken);

        _logger.LogInformation(
            "Store reported {Status} on restore ({Count} active)",
            result.Error, result.Value?.ActiveSubscriptions?.Count ?? 0);

        if (result.IsError || result.Value is null)
        {
            // Note the asymmetry with PurchaseAsync: outside a purchase, a "cancelled"
            // status means our own CancellationToken fired, not that the user backed out.
            return cancellationToken.IsCancellationRequested
                ? new PurchaseResult { Outcome = PurchaseOutcome.Cancelled }
                : Failed(result.Error, result.ErrorException, productId: null);
        }

        var active = result.Value.ActiveSubscriptions;

        if (active is not { Count: > 0 })
        {
            return new PurchaseResult { Outcome = PurchaseOutcome.NothingToRestore };
        }

        return new PurchaseResult { Outcome = PurchaseOutcome.Restored, ProductId = active[0] };
    }

    /// <summary>
    /// Where the store lets someone manage or cancel this subscription, or null if it
    /// will not say.
    /// </summary>
    /// <remarks>
    /// Not on <see cref="IPurchaseService"/> because it is a link, not a purchase. Apple
    /// and Google own cancellation for their own stores and there is no API to do it from
    /// here — sending someone to this URL is the only thing the app can honestly offer.
    /// </remarks>
    public async Task<string?> GetManagementUrlAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return null;

        var result = await _billing.GetManagementSubscriptionUrl(cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("Could not read the store's manage-subscription URL: {Error}", result.Error);
            return null;
        }

        return result.Value;
    }

    /// <summary>
    /// Unlinks the store from this household.
    /// </summary>
    /// <remarks>
    /// Called when the app is reset or signed out. Skipping it leaves the SDK pointed at
    /// the previous tenant, so the next household's purchases would be attributed to it.
    /// </remarks>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (!_billing.IsInitialized()) return;

        try
        {
            await _billing.Logout(cancellationToken);
        }
        catch (Exception ex)
        {
            // Best effort. A failure here must not block signing out.
            _logger.LogWarning(ex, "Could not sign out of the store");
        }
    }

    // ---------- mapping ----------

    private static SubscriptionPlan ToPlan(PackageDto package, PlanCopy copy)
    {
        var sku = package.Product!.Sku;
        var entry = copy.For(sku);

        return new SubscriptionPlan
        {
            ProductId = sku,
            // Falls back to the raw product id rather than showing nothing. Ugly beats blank,
            // and it makes a missing metadata entry obvious instead of invisible.
            Title = entry?.Title ?? sku,
            Description = entry?.Description,
            // The store's own localized string, never a price we compose. It is the amount
            // the customer will actually be charged, in their storefront's currency, and it
            // stays right through a price change we have not shipped for.
            Price = package.Product.Pricing?.PriceLocalized ?? string.Empty,
            Period = ToPeriod(package),
            AdvertisedTier = entry?.Tier
        };
    }

    private static BillingPeriod ToPeriod(PackageDto package) =>
        package.Identifier switch
        {
            DefaultPackageIdentifier.Monthly => BillingPeriod.Monthly,
            DefaultPackageIdentifier.Annually => BillingPeriod.Annual,
            _ => package.Product?.SubscriptionPeriod?.Unit switch
            {
                SubscriptionUnit.Month => BillingPeriod.Monthly,
                SubscriptionUnit.Year => BillingPeriod.Annual,
                _ => BillingPeriod.Unknown
            }
        };

    private static PurchaseResult Failure(string message) =>
        new() { Outcome = PurchaseOutcome.Failed, Message = message };

    /// <summary>
    /// Turns a store error into something worth showing someone.
    /// </summary>
    /// <remarks>
    /// The underlying exception is logged and never surfaced — it carries backend codes and
    /// wording written for developers.
    /// </remarks>
    private PurchaseResult Failed(PurchaseErrorStatus? status, Exception? exception, string? productId)
    {
        _logger.LogError(exception, "Store purchase failed with {Status} for {ProductId}", status, productId);

        var message = status switch
        {
            PurchaseErrorStatus.NetworkError or PurchaseErrorStatus.OfflineConnectionError =>
                "We couldn't reach the store. Check your connection and try again.",

            PurchaseErrorStatus.StoreProblemError or PurchaseErrorStatus.UnknownBackendError =>
                "The store isn't responding right now. Please try again in a few minutes.",

            PurchaseErrorStatus.PurchaseNotAllowedError =>
                "Purchases are restricted on this device.",

            PurchaseErrorStatus.ProductNotAvailableForPurchaseError =>
                "That plan isn't available in your region right now.",

            // The store account already owns this subscription under a different household.
            // Restoring is the honest path; buying again would charge twice for one thing.
            PurchaseErrorStatus.PurchaseBelongsToOtherUser or PurchaseErrorStatus.ReceiptAlreadyInUseError =>
                "This subscription is already linked to another household. Try Restore Purchases, or contact support.",

            PurchaseErrorStatus.ConfigurationError or PurchaseErrorStatus.InvalidAppUserIdError =>
                "Something is wrong with our store setup. Please contact support.",

            _ => "Something went wrong with the purchase. You have not been charged."
        };

        return new PurchaseResult { Outcome = PurchaseOutcome.Failed, Message = message, ProductId = productId };
    }
}
