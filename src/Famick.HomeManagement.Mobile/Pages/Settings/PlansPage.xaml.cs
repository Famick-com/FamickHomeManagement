using CommunityToolkit.Maui;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Mobile.Services;
using Famick.HomeManagement.Shared.Authentication;

namespace Famick.HomeManagement.Mobile.Pages.Settings;

/// <summary>
/// The plans screen: what the household is on, what the store will sell them, and how to
/// restore a purchase they already made.
/// </summary>
/// <remarks>
/// Reachable from Settings at any time, during a trial or not. That is deliberate and not
/// merely convenient — a purchase surface that only appears once something is refused
/// cannot be found by an App Store reviewer, who creates a fresh account, gets a full trial,
/// and reports that the in-app purchases could not be located.
///
/// <para>Cloud households only. Self-hosted and proxied households run their own server,
/// owe nothing, and must not be offered a subscription — the Settings entry is hidden for
/// them, and <see cref="OnAppearing"/> refuses the page as well in case anything else
/// navigates here.</para>
/// </remarks>
public partial class PlansPage : ContentPage, IQueryAttributable
{
    private readonly IPurchaseService _purchases;
    private readonly SubscriptionStateService _subscriptionState;
    private readonly ShoppingApiClient _apiClient;
    private readonly TokenStorage _tokenStorage;
    private readonly ApiSettings _apiSettings;

    /// <summary>
    /// How long to wait for the server to hear about a purchase, and how often to ask.
    /// </summary>
    /// <remarks>
    /// The store tells RevenueCat, which tells the cloud. Usually seconds, with no promise
    /// and no guarantee it arrives at all. The first gap is not zero because asking the
    /// instant the sheet closes is certain to be too early; the rest back off so a slow
    /// hand-off is still caught without hammering the server for a minute.
    /// </remarks>
    private static readonly int[] PollDelaysMs = [1500, 2000, 3000, 5000, 8000, 10000, 10000, 10000, 10000];

    private CancellationTokenSource? _pollCts;
    private bool _isAdmin;

    /// <summary>
    /// Guards against two overlapping loads. Returning from the in-app browser re-fires
    /// <see cref="OnAppearing"/>, and two runs can both clear the plan list and then both
    /// append to it, showing every plan twice.
    /// </summary>
    private bool _isLoading;

    /// <summary>
    /// The tier the user was trying to reach when they were stopped, when they arrived from
    /// the upgrade prompt rather than from Settings.
    /// </summary>
    /// <remarks>
    /// Used to mark the plan that would actually unlock what they wanted. Someone sent here
    /// by a locked feature has a specific need, and making them work out which plan covers
    /// it is a good way to lose them.
    /// </remarks>
    private SubscriptionTier? _highlightTier;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("highlightTier", out var raw)
            && Enum.TryParse<SubscriptionTier>(raw?.ToString(), ignoreCase: true, out var tier))
        {
            _highlightTier = tier;
        }
    }

    public PlansPage(
        IPurchaseService purchases,
        SubscriptionStateService subscriptionState,
        ShoppingApiClient apiClient,
        TokenStorage tokenStorage,
        ApiSettings apiSettings)
    {
        InitializeComponent();
        _purchases = purchases;
        _subscriptionState = subscriptionState;
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _apiSettings = apiSettings;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_apiSettings.IsSelfHostedServer())
        {
            // Nothing to sell. Leave rather than showing an empty shop.
            await Navigation.PopAsync();
            return;
        }

        if (_isLoading) return;
        _isLoading = true;

        try
        {
            _isAdmin = _tokenStorage.HasAdminRole();
            NonAdminNoticeLabel.IsVisible = !_isAdmin;

            // Always re-read rather than trusting what is cached: another member may have
            // subscribed since this device last looked, and offering to sell a plan the
            // household already pays for is how someone ends up paying twice.
            await _subscriptionState.RefreshAsync();

            await ReadPlatformAsync();
            await ShowCurrentPlanAsync();
            await LoadPlansAsync();
        }
        catch (Exception ex)
        {
            // The store SDK talks to native code and can throw. On an async void handler
            // that takes the process down, so it stops here.
            Console.WriteLine($"[PlansPage] Load failed: {ex.Message}");
            ShowLoadFailure();
        }
        finally
        {
            _isLoading = false;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // A poll that outlives the page would keep calling the server and then try to
        // update controls nobody is looking at.
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    // ---------- current plan ----------

    private async Task ShowCurrentPlanAsync()
    {
        var tenant = await ReadTenantAsync();

        // Deliberately not SubscriptionStateService.CurrentTier: it answers Pro for a
        // household whose stored tier is empty, which is a safe default for feature gating
        // and a plain untruth on a screen about money.
        var tier = tenant?.SubscriptionTier;

        CurrentPlanLabel.Text = string.IsNullOrWhiteSpace(tier) ? "Unknown" : tier;

        CurrentPlanDetailLabel.Text = tenant switch
        {
            null => "We couldn't check your plan just now. Pull down to try again.",
            { IsTrialActive: true, TrialEndsAt: { } ends } =>
                $"Free trial — ends {ends.ToLocalTime():d MMMM yyyy}",
            { IsTrialActive: true } => "Free trial",
            { IsExpired: true } => "Your subscription has ended. Your data is safe and still readable.",
            _ => DescribePlatform()
        };

        await ShowManageButtonAsync();
    }

    /// <summary>
    /// Names who is billing, so the screen never offers a control that platform will not
    /// honour.
    /// </summary>
    private string DescribePlatform() => PlatformOwner switch
    {
        BillingPlatform.AppStore => "Billed through the App Store",
        BillingPlatform.GooglePlay => "Billed through Google Play",
        BillingPlatform.Stripe => "Billed on the web — manage it at famick.com",
        BillingPlatform.Test => "Test purchase — not a real subscription",
        _ => "Active"
    };

    private BillingPlatform? PlatformOwner { get; set; }

    /// <summary>
    /// Reads who is billing the household.
    /// </summary>
    /// <remarks>
    /// A separate call from the tenant read because the tenant endpoint is shared with the
    /// self-hosted server and carries no billing platform — only the cloud knows this, and
    /// only the cloud is ever asked.
    ///
    /// <para>Failure is not worth reporting: without it the screen simply says "Active"
    /// instead of naming a store, and hides a manage button that might not have worked
    /// anyway.</para>
    /// </remarks>
    private async Task ReadPlatformAsync()
    {
        if (!_apiSettings.IsCloudServer()) return;

        try
        {
            var result = await _apiClient.GetSubscriptionAsync();
            PlatformOwner = result.Success ? result.Data?.Platform : null;
        }
        catch
        {
            PlatformOwner = null;
        }
    }

    private async Task ShowManageButtonAsync()
    {
        // Only the stores have somewhere to send people. A web subscription is managed at
        // famick.com, which the link at the bottom of the page already covers.
        if (PlatformOwner is not (BillingPlatform.AppStore or BillingPlatform.GooglePlay))
        {
            ManageButton.IsVisible = false;
            return;
        }

        try
        {
            ManageButton.IsVisible = _purchases is PurchaseService concrete
                && await concrete.GetManagementUrlAsync() is not null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlansPage] Could not read store management URL: {ex.Message}");
            ManageButton.IsVisible = false;
        }
    }

    private async Task<TenantInfoDto?> ReadTenantAsync()
    {
        try
        {
            var result = await _apiClient.GetTenantAsync();
            return result.Success ? result.Data : null;
        }
        catch
        {
            return null;
        }
    }

    // ---------- the plans on offer ----------

    private async Task LoadPlansAsync()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;

        IReadOnlyList<SubscriptionPlan> plans;

        try
        {
            plans = await _purchases.GetPlansAsync();
        }
        finally
        {
            // Otherwise a throw leaves the spinner running forever.
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }

        var cards = PlanPresentation.GroupIntoTierCards(plans);

        if (cards.Count == 0)
        {
            // Empty is a normal answer here, not a failure — see IPurchaseService.
            EmptyStateCard.IsVisible = true;
            PlansContainer.IsVisible = false;
            return;
        }

        EmptyStateCard.IsVisible = false;
        PlansContainer.Children.Clear();

        foreach (var card in cards)
        {
            PlansContainer.Children.Add(BuildCardView(card));
        }

        PlansContainer.IsVisible = true;
    }

    private View BuildCardView(PlanPresentation.TierCard card)
    {
        var content = new VerticalStackLayout { Spacing = 8 };

        content.Children.Add(Themed(new Label
        {
            Text = card.Title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }, Label.TextColorProperty, "TextPrimary"));

        if (!string.IsNullOrWhiteSpace(card.Description))
        {
            content.Children.Add(Themed(new Label
            {
                Text = card.Description,
                FontSize = 14,
                LineBreakMode = LineBreakMode.WordWrap
            }, Label.TextColorProperty, "TextMuted"));
        }

        foreach (var feature in card.Features)
        {
            content.Children.Add(Themed(new Label
            {
                Text = $"• {feature}",
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap
            }, Label.TextColorProperty, "TextMuted"));
        }

        foreach (var plan in new[] { card.Monthly, card.Annual }.Concat(card.Other))
        {
            if (plan is null) continue;
            content.Children.Add(BuildPurchaseRow(plan));
        }

        var isHighlighted = _highlightTier.HasValue && card.Tier == _highlightTier;

        if (isHighlighted)
        {
            content.Children.Insert(0, Themed(new Label
            {
                Text = "UNLOCKS WHAT YOU TRIED TO OPEN",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold
            }, Label.TextColorProperty, "BrandForeground"));
        }

        var border = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            StrokeThickness = isHighlighted ? 2 : 1,
            Margin = new Thickness(15, 0),
            Padding = 16,
            Content = content
        };

        Themed(border, Border.BackgroundColorProperty, "Surface");

        var strokeKey = isHighlighted ? "BrandForeground" : "Divider";
        var (strokeLight, strokeDark) = ThemePair(strokeKey);
        border.SetAppTheme<Brush>(Border.StrokeProperty,
            new SolidColorBrush(strokeLight), new SolidColorBrush(strokeDark));

        return border;
    }

    private View BuildPurchaseRow(SubscriptionPlan plan)
    {
        var label = plan.Period switch
        {
            BillingPeriod.Monthly => $"{plan.Price} / month",
            BillingPeriod.Annual => $"{plan.Price} / year",
            _ => plan.Price
        };

        // Non-admins see the price but get no button. The household's billing is not
        // theirs to change, and this keeps two members from buying at the same moment.
        if (!_isAdmin)
        {
            return Themed(new Label
            {
                Text = label,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 8, 0, 0)
            }, Label.TextColorProperty, "TextPrimary");
        }

        var button = new Button
        {
            Text = label,
            CornerRadius = 8,
            Margin = new Thickness(0, 8, 0, 0),
            BackgroundColor = Colors.SeaGreen,
            TextColor = Colors.White
        };

        button.Clicked += async (_, _) =>
        {
            try
            {
                await PurchaseAsync(plan);
            }
            catch (Exception ex)
            {
                // The store SDK wraps native code and can throw. Unhandled on an async
                // handler that ends the process — in the middle of taking a payment.
                Console.WriteLine($"[PlansPage] Purchase failed: {ex.Message}");
                ClearBusy();
                await DisplayAlert(
                    "Purchase didn't complete",
                    "Something went wrong talking to the store. If you were charged, "
                    + "use Restore Purchases or contact support.",
                    "OK");
            }
        };

        return button;
    }

    /// <summary>
    /// The light and dark values of a themed colour resource.
    /// </summary>
    /// <remarks>
    /// The palette in <c>Resources/Styles/Colors.xaml</c> is declared as
    /// <c>AppThemeColor</c>, not <c>Color</c> — XAML resolves that through the
    /// <c>AppThemeResource</c> markup extension, but code reading the dictionary gets the
    /// wrapper and has to unpack it. Treating the entry as a <c>Color</c> silently misses
    /// and leaves everything built here a flat grey that ignores the theme.
    /// </remarks>
    private static (Color Light, Color Dark) ThemePair(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true)
        {
            switch (value)
            {
                case AppThemeColor themed:
                    return (themed.Light ?? themed.Default ?? Colors.Grey,
                            themed.Dark ?? themed.Default ?? Colors.Grey);
                case Color color:
                    return (color, color);
            }
        }

        return (Colors.Grey, Colors.Grey);
    }

    /// <summary>
    /// Binds a themed colour so it keeps following the system theme.
    /// </summary>
    /// <remarks>
    /// These views are built in code rather than XAML, so they get no theme binding for
    /// free. Reading the current theme once and assigning a fixed colour would look right
    /// until someone switched appearance with the page open.
    /// </remarks>
    private static T Themed<T>(T view, BindableProperty property, string key)
        where T : VisualElement
    {
        var (light, dark) = ThemePair(key);
        view.SetAppTheme(property, light, dark);
        return view;
    }

    // ---------- buying ----------

    private async Task PurchaseAsync(SubscriptionPlan plan)
    {
        // Snapshot before the sheet opens. The poll compares against this rather than
        // against a tier we expect, because what a product is worth is the server's
        // decision and not ours to predict.
        var before = await ReadTenantAsync();

        SetBusy("Opening the store…");

        var result = await _purchases.PurchaseAsync(plan.ProductId);

        switch (result.Outcome)
        {
            case PurchaseOutcome.Purchased:
            case PurchaseOutcome.Restored:
                await WaitForEntitlementAsync(before, plan.AdvertisedTier);
                break;

            case PurchaseOutcome.Pending:
                // Ask to Buy, or a bank still checking. Nothing has gone wrong and nothing
                // is owed yet, so there is nothing to wait for on this screen.
                ClearBusy();
                await DisplayAlert(
                    "Waiting for approval",
                    "Your purchase needs approval before it can complete. Your plan will update once it's approved.",
                    "OK");
                break;

            case PurchaseOutcome.Cancelled:
                // Changing your mind is not an error and is not worth a dialog.
                ClearBusy();
                break;

            default:
                ClearBusy();
                await DisplayAlert(
                    "Purchase didn't complete",
                    result.Message ?? "Something went wrong with the purchase. You have not been charged.",
                    "OK");
                break;
        }
    }

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        try
        {
            await RestoreAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlansPage] Restore failed: {ex.Message}");
            ClearBusy();
            await DisplayAlert(
                "Couldn't restore",
                "Something went wrong talking to the store. Please try again.",
                "OK");
        }
    }

    private async Task RestoreAsync()
    {
        var before = await ReadTenantAsync();

        SetBusy("Checking for previous purchases…");

        var result = await _purchases.RestoreAsync();

        switch (result.Outcome)
        {
            case PurchaseOutcome.Restored:
                await WaitForEntitlementAsync(before, advertised: null);
                break;

            case PurchaseOutcome.NothingToRestore:
                ClearBusy();
                await DisplayAlert(
                    "Nothing to restore",
                    "We couldn't find a previous subscription on this store account.",
                    "OK");
                break;

            case PurchaseOutcome.Cancelled:
                ClearBusy();
                break;

            default:
                ClearBusy();
                await DisplayAlert(
                    "Couldn't restore",
                    result.Message ?? "We couldn't check for previous purchases. Please try again.",
                    "OK");
                break;
        }
    }

    /// <summary>
    /// Waits for the server to reflect a purchase the store has already taken.
    /// </summary>
    /// <remarks>
    /// The client never decides its own entitlement, so this asks and compares rather than
    /// assuming. If the wait runs out it says the purchase went through and that the plan
    /// is still updating — because it did, and it is. Telling someone their payment failed
    /// after taking their money is both untrue and the kind of thing that gets a build
    /// rejected.
    /// </remarks>
    /// <param name="advertised">
    /// The tier the card said this plan was, when there was one. Only used to notice a
    /// disagreement — the server's answer is the one shown either way.
    /// </param>
    private async Task WaitForEntitlementAsync(TenantInfoDto? before, SubscriptionTier? advertised)
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;

        // If the snapshot taken before the sheet opened failed, fall back to what the app
        // already believes. Comparing against nothing can never register a change, which
        // would send every purchase down the timeout path even when it landed instantly.
        var tierBefore = before?.SubscriptionTier ?? _subscriptionState.CurrentTier.ToString();
        var expiredBefore = before?.IsExpired ?? _subscriptionState.IsExpired;

        SetBusy("Activating your subscription…");

        var settled = false;

        try
        {
            foreach (var delay in PollDelaysMs)
            {
                await Task.Delay(delay, token);

                var after = await ReadTenantAsync();

                // ReadTenantAsync is not cancellable, so the page can have gone while it
                // was in flight. Anything below this touches the UI.
                if (token.IsCancellationRequested) return;

                if (after is null) continue;

                if (!PlanPresentation.HasUpgraded(
                        tierBefore, expiredBefore, after.SubscriptionTier, after.IsExpired))
                {
                    continue;
                }

                settled = true;

                await _subscriptionState.RefreshAsync();
                await ReadPlatformAsync();
                if (token.IsCancellationRequested) return;

                ClearBusy();
                await ShowCurrentPlanAsync();
                await LoadPlansAsync();

                // The screen takes its tier from store metadata; the server takes it from
                // the entitlement the store actually granted. Nothing keeps those two in
                // step, so if they disagree the customer has been shown one plan and given
                // another — worth saying out loud rather than quietly displaying the tier
                // they did not choose.
                var mismatched = advertised.HasValue
                    && Enum.TryParse<SubscriptionTier>(after.SubscriptionTier, true, out var granted)
                    && granted != advertised.Value;

                if (mismatched)
                {
                    Console.WriteLine(
                        $"[PlansPage] Advertised {advertised} but the server granted "
                        + $"{after.SubscriptionTier} — store metadata and the entitlement "
                        + "mapping disagree.");

                    await DisplayAlert(
                        "Your plan is active",
                        $"Your household is now on {after.SubscriptionTier}. If you expected "
                        + $"{advertised}, please contact support — we'll put it right.",
                        "OK");
                    return;
                }

                await DisplayAlert(
                    "You're all set",
                    $"Your household is now on {after.SubscriptionTier}.",
                    "Great");
                return;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlansPage] Entitlement poll failed: {ex.Message}");
        }
        finally
        {
            // Without this, cancelling mid-poll — which happens whenever the page is left,
            // including by opening a link on it — strands the screen showing "Activating…"
            // with its buttons disabled and no poll running, after the money has been taken.
            if (!settled) ClearBusy();
        }

        if (token.IsCancellationRequested) return;

        // Ran out of patience, not out of money.
        StatusCard.IsVisible = true;
        StatusSpinner.IsVisible = false;
        StatusSpinner.IsRunning = false;
        StatusLabel.Text =
            "Your purchase went through. It can take a minute or two for your plan to update — "
            + "we'll keep checking, and Restore Purchases will also sort it out.";
        CheckAgainButton.IsVisible = true;
    }

    /// <summary>Shown when the store or the server could not be reached at all.</summary>
    private void ShowLoadFailure()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        EmptyStateCard.IsVisible = true;
        EmptyStateDetailLabel.Text =
            "We couldn't reach the store just now. Check your connection and try again, "
            + "or subscribe on the web.";
    }

    private async void OnCheckAgainClicked(object? sender, EventArgs e)
    {
        CheckAgainButton.IsVisible = false;

        try
        {
            await _subscriptionState.RefreshAsync();
            await ReadPlatformAsync();
            await ShowCurrentPlanAsync();
            await LoadPlansAsync();
            StatusCard.IsVisible = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlansPage] Re-check failed: {ex.Message}");
            CheckAgainButton.IsVisible = true;
        }
    }

    // ---------- links ----------

    private async void OnManageClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_purchases is not PurchaseService concrete) return;

            var url = await concrete.GetManagementUrlAsync();
            if (string.IsNullOrEmpty(url)) return;

            await OpenAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlansPage] Could not open store management: {ex.Message}");
        }
    }

    private async void OnWebBillingTapped(object? sender, TappedEventArgs e) =>
        await OpenAsync("https://app.famick.com/settings/billing");

    private async void OnTermsTapped(object? sender, TappedEventArgs e) =>
        await OpenAsync("https://famick.com/terms");

    private async void OnPrivacyTapped(object? sender, TappedEventArgs e) =>
        await OpenAsync("https://famick.com/privacy");

    private static async Task OpenAsync(string url)
    {
        try
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            // No browser, or the user dismissed it. Nothing to recover.
        }
    }

    // ---------- busy state ----------

    private void SetBusy(string message)
    {
        StatusCard.IsVisible = true;
        StatusSpinner.IsVisible = true;
        StatusSpinner.IsRunning = true;
        StatusLabel.Text = message;
        CheckAgainButton.IsVisible = false;
        PlansContainer.IsEnabled = false;
        RestoreButton.IsEnabled = false;
    }

    private void ClearBusy()
    {
        StatusCard.IsVisible = false;
        StatusSpinner.IsRunning = false;
        PlansContainer.IsEnabled = true;
        RestoreButton.IsEnabled = true;
    }
}
