using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using static Famick.HomeManagement.Mobile.Pages.Profile.ProfileUiHelpers;

namespace Famick.HomeManagement.Mobile.Pages.Profile;

public partial class ProfileCalendarPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly CalendarSyncOrchestrator _orchestrator;
    private List<ExternalCalendarSubscriptionMobile>? _subscriptions;
    private List<IcsTokenMobile>? _icsTokens;
    private CalendarSyncStatus? _syncStatus;
    private bool _loaded;
    private bool _isSyncing;

    public ProfileCalendarPage(ShoppingApiClient apiClient, CalendarSyncOrchestrator orchestrator)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _orchestrator = orchestrator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded)
        {
            await LoadCalendarDataAsync();
            _loaded = true;
        }
    }

    private async Task LoadCalendarDataAsync()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            var subsTask = _apiClient.GetCalendarSubscriptionsAsync();
            var tokensTask = _apiClient.GetIcsTokensAsync();
            var statusTask = _orchestrator.GetStatusAsync();
            await Task.WhenAll(subsTask, tokensTask, statusTask);

            if (subsTask.Result.Success)
                _subscriptions = subsTask.Result.Data;
            if (tokensTask.Result.Success)
                _icsTokens = tokensTask.Result.Data;
            _syncStatus = statusTask.Result;

            RenderCalendar();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load calendar data: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void RenderCalendar()
    {
        CalendarStack.Children.Clear();

        // Device Calendar Sync section
        RenderDeviceSyncSection();

        // Divider
        CalendarStack.Children.Add(CreateDivider());

        // External Subscriptions section
        RenderExternalSubscriptionsSection();

        // Divider
        CalendarStack.Children.Add(CreateDivider());

        // ICS Feed Tokens section
        RenderFeedTokensSection();
    }

    #region Device Calendar Sync

    private void RenderDeviceSyncSection()
    {
        CalendarStack.Children.Add(CreateLabel("Device Calendar Sync", true, 18));
        CalendarStack.Children.Add(CreateLabel(
            "Sync Famick events to this device's calendar. Events added to the Famick calendar from your device's Calendar app will also sync back.",
            false, 13));

        // Sync toggle
        var syncEnabled = CalendarSyncOrchestrator.IsSyncEnabled;
        var toggleSwitch = new Switch
        {
            IsToggled = syncEnabled,
            OnColor = Color.FromArgb("#1976D2")
        };
        toggleSwitch.Toggled += OnDeviceSyncToggled;

        CalendarStack.Children.Add(CreateCard(new HorizontalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 4,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    Children =
                    {
                        CreateLabel("Enable Calendar Sync", true),
                        CreateLabel("Automatically sync events to this device", false, 12)
                    }
                },
                toggleSwitch
            }
        }));

        // Status + actions (only when sync is enabled or has been used)
        if (syncEnabled || (_syncStatus?.SyncedCount ?? 0) > 0)
        {
            var lastSynced = _syncStatus?.LastSyncedAt?.ToLocalTime().ToString("g") ?? "Never";
            var syncedCount = _syncStatus?.SyncedCount ?? 0;

            CalendarStack.Children.Add(CreateCard(new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    CreateLabel($"Synced events: {syncedCount}", false, 14),
                    CreateLabel($"Last synced: {lastSynced}", false, 14)
                }
            }));

            var buttonsLayout = new HorizontalStackLayout { Spacing = 8 };

            var syncNowBtn = new Button
            {
                Text = _isSyncing ? "Syncing..." : "Sync Now",
                BackgroundColor = Color.FromArgb("#1976D2"),
                TextColor = Colors.White,
                CornerRadius = 6,
                FontSize = 13,
                Padding = new Thickness(15, 8),
                IsEnabled = !_isSyncing && _syncStatus?.HasPermission == true
            };
            syncNowBtn.Clicked += OnSyncNowClicked;
            buttonsLayout.Children.Add(syncNowBtn);

            if (syncedCount > 0)
            {
                var removeBtn = new Button
                {
                    Text = "Remove All",
                    BackgroundColor = Color.FromArgb("#D32F2F"),
                    TextColor = Colors.White,
                    CornerRadius = 6,
                    FontSize = 13,
                    Padding = new Thickness(15, 8),
                    IsEnabled = !_isSyncing
                };
                removeBtn.Clicked += OnRemoveAllSyncedClicked;
                buttonsLayout.Children.Add(removeBtn);
            }

            CalendarStack.Children.Add(buttonsLayout);
        }
    }

    private async void OnDeviceSyncToggled(object? sender, ToggledEventArgs e)
    {
        if (e.Value)
        {
            // Request permission when enabling sync if not already granted
            var syncService = GetCalendarSyncService();
            if (syncService != null && _syncStatus?.HasPermission != true)
            {
                var granted = await syncService.RequestPermissionAsync();
                if (!granted)
                {
                    if (sender is Switch toggle)
                        toggle.IsToggled = false;
                    return;
                }

                _syncStatus = await _orchestrator.GetStatusAsync();
            }

            CalendarSyncOrchestrator.IsSyncEnabled = true;
#if IOS
            Platforms.iOS.BackgroundCalendarSyncTask.ScheduleNextSync();
#elif ANDROID
            Platforms.Android.CalendarSyncWorker.Schedule();
#endif
        }
        else
        {
            CalendarSyncOrchestrator.IsSyncEnabled = false;
#if IOS
            Platforms.iOS.BackgroundCalendarSyncTask.CancelScheduledSync();
#elif ANDROID
            Platforms.Android.CalendarSyncWorker.Cancel();
#endif
        }

        RenderCalendar();
    }

    private async void OnSyncNowClicked(object? sender, EventArgs e)
    {
        _isSyncing = true;
        RenderCalendar();

        try
        {
            var result = await _orchestrator.SyncAsync();
            if (result.Success)
            {
                var message = $"Created: {result.Created}, Updated: {result.Updated}, Deleted: {result.Deleted}";
                if (result.Failed > 0)
                    message += $", Failed: {result.Failed}";
                await DisplayAlert("Sync Complete", message, "OK");
            }
            else
            {
                await DisplayAlert("Sync Failed", result.ErrorMessage ?? "Unknown error", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Sync failed: {ex.Message}", "OK");
        }
        finally
        {
            _isSyncing = false;
            _loaded = false;
            await LoadCalendarDataAsync();
        }
    }

    private async void OnRemoveAllSyncedClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Remove All",
            "Remove all Famick-synced events from your device's Calendar app and delete the Famick calendar?",
            "Remove All",
            "Cancel");

        if (!confirm) return;

        _isSyncing = true;
        RenderCalendar();

        try
        {
            var result = await _orchestrator.RemoveAllAsync();
            if (result.Success)
                await DisplayAlert("Done", $"Removed {result.Deleted} events from device", "OK");
            else
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to remove events", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Remove failed: {ex.Message}", "OK");
        }
        finally
        {
            _isSyncing = false;
            _loaded = false;
            await LoadCalendarDataAsync();
        }
    }

    #endregion

    #region External Subscriptions

    private void RenderExternalSubscriptionsSection()
    {
        CalendarStack.Children.Add(CreateLabel("External Subscriptions", true, 18));

        if (_subscriptions != null && _subscriptions.Count > 0)
        {
            foreach (var sub in _subscriptions)
            {
                var truncatedUrl = sub.IcsUrl.Length > 40 ? sub.IcsUrl[..40] + "..." : sub.IcsUrl;
                var lastSynced = sub.LastSyncedAt?.ToString("g") ?? "Never";
                var status = sub.IsActive ? "Active" : "Inactive";

                var syncBtn = new Button
                {
                    Text = "Sync",
                    BackgroundColor = Color.FromArgb("#1976D2"),
                    TextColor = Colors.White,
                    CornerRadius = 6,
                    FontSize = 12,
                    Padding = new Thickness(10, 5),
                    CommandParameter = sub.Id
                };
                syncBtn.Clicked += OnSyncSubscriptionClicked;

                var deleteBtn = new Button
                {
                    Text = "Delete",
                    BackgroundColor = Color.FromArgb("#D32F2F"),
                    TextColor = Colors.White,
                    CornerRadius = 6,
                    FontSize = 12,
                    Padding = new Thickness(10, 5),
                    CommandParameter = sub.Id
                };
                deleteBtn.Clicked += OnDeleteSubscriptionClicked;

                CalendarStack.Children.Add(CreateCard(new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        CreateLabel(sub.Name, true),
                        CreateLabel(truncatedUrl, false, 12),
                        CreateLabel($"Last synced: {lastSynced} | Status: {status}", false, 12),
                        CreateLabel($"{sub.EventCount} events | Sync every {sub.SyncIntervalMinutes} min", false, 12),
                        new HorizontalStackLayout
                        {
                            Spacing = 8,
                            Children = { syncBtn, deleteBtn }
                        }
                    }
                }));
            }
        }
        else
        {
            CalendarStack.Children.Add(CreateLabel("No external calendar subscriptions", false, 14));
        }

        var addSubBtn = new Button
        {
            Text = "+ Add Subscription",
            BackgroundColor = Color.FromArgb("#1976D2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 10)
        };
        addSubBtn.Clicked += OnAddSubscriptionClicked;
        CalendarStack.Children.Add(addSubBtn);
    }

    private async void OnSyncSubscriptionClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Guid id })
        {
            var result = await _apiClient.SyncCalendarSubscriptionAsync(id);
            if (result.Success)
            {
                await DisplayAlert("Success", "Sync started", "OK");
                await LoadCalendarDataAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to sync", "OK");
            }
        }
    }

    private async void OnDeleteSubscriptionClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Guid id })
        {
            var confirm = await DisplayAlert("Delete", "Delete this subscription?", "Yes", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.DeleteCalendarSubscriptionAsync(id);
            if (result.Success)
                await LoadCalendarDataAsync();
            else
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete", "OK");
        }
    }

    private async void OnAddSubscriptionClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Add Subscription", "Subscription name:");
        if (string.IsNullOrWhiteSpace(name)) return;

        var url = await DisplayPromptAsync("Add Subscription", "ICS URL:");
        if (string.IsNullOrWhiteSpace(url)) return;

        var request = new CreateCalendarSubscriptionMobileRequest
        {
            Name = name,
            IcsUrl = url
        };

        var result = await _apiClient.CreateCalendarSubscriptionAsync(request);
        if (result.Success)
            await LoadCalendarDataAsync();
        else
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create subscription", "OK");
    }

    #endregion

    #region Feed Tokens

    private void RenderFeedTokensSection()
    {
        CalendarStack.Children.Add(CreateLabel("Calendar Feed Tokens", true, 18));

        if (_icsTokens != null && _icsTokens.Count > 0)
        {
            foreach (var token in _icsTokens)
            {
                var statusText = token.IsRevoked ? "Revoked" : "Active";
                var statusColor = token.IsRevoked ? Color.FromArgb("#D32F2F") : Color.FromArgb("#4CAF50");

                var buttons = new HorizontalStackLayout { Spacing = 8 };

                if (!string.IsNullOrEmpty(token.FeedUrl))
                {
                    var copyBtn = new Button
                    {
                        Text = "Copy URL",
                        BackgroundColor = Color.FromArgb("#757575"),
                        TextColor = Colors.White,
                        CornerRadius = 6,
                        FontSize = 12,
                        Padding = new Thickness(10, 5),
                        CommandParameter = token.FeedUrl
                    };
                    copyBtn.Clicked += OnCopyFeedUrlClicked;
                    buttons.Children.Add(copyBtn);
                }

                if (!token.IsRevoked)
                {
                    var revokeBtn = new Button
                    {
                        Text = "Revoke",
                        BackgroundColor = Color.FromArgb("#FF9800"),
                        TextColor = Colors.White,
                        CornerRadius = 6,
                        FontSize = 12,
                        Padding = new Thickness(10, 5),
                        CommandParameter = token.Id
                    };
                    revokeBtn.Clicked += OnRevokeTokenClicked;
                    buttons.Children.Add(revokeBtn);
                }

                var deleteTokenBtn = new Button
                {
                    Text = "Delete",
                    BackgroundColor = Color.FromArgb("#D32F2F"),
                    TextColor = Colors.White,
                    CornerRadius = 6,
                    FontSize = 12,
                    Padding = new Thickness(10, 5),
                    CommandParameter = token.Id
                };
                deleteTokenBtn.Clicked += OnDeleteTokenClicked;
                buttons.Children.Add(deleteTokenBtn);

                CalendarStack.Children.Add(CreateCard(new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        CreateLabel(token.Label ?? "Unnamed Token", true),
                        new Label
                        {
                            Text = statusText,
                            FontSize = 12,
                            TextColor = statusColor
                        },
                        CreateLabel($"Created: {token.CreatedAt:g}", false, 12),
                        buttons
                    }
                }));
            }
        }
        else
        {
            CalendarStack.Children.Add(CreateLabel("No calendar feed tokens", false, 14));
        }

        var createTokenBtn = new Button
        {
            Text = "+ Create Token",
            BackgroundColor = Color.FromArgb("#1976D2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 10),
            Margin = new Thickness(0, 0, 0, 20)
        };
        createTokenBtn.Clicked += OnCreateTokenClicked;
        CalendarStack.Children.Add(createTokenBtn);
    }

    private async void OnCopyFeedUrlClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string url })
        {
            await Clipboard.SetTextAsync(url);
            await DisplayAlert("Copied", "Feed URL copied to clipboard", "OK");
        }
    }

    private async void OnRevokeTokenClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Guid id })
        {
            var confirm = await DisplayAlert("Revoke", "Revoke this token? It will no longer work for calendar feeds.", "Yes", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.RevokeIcsTokenAsync(id);
            if (result.Success)
                await LoadCalendarDataAsync();
            else
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to revoke token", "OK");
        }
    }

    private async void OnDeleteTokenClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Guid id })
        {
            var confirm = await DisplayAlert("Delete", "Delete this token?", "Yes", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.DeleteIcsTokenAsync(id);
            if (result.Success)
                await LoadCalendarDataAsync();
            else
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete token", "OK");
        }
    }

    private async void OnCreateTokenClicked(object? sender, EventArgs e)
    {
        var label = await DisplayPromptAsync("Create Token", "Token label (optional):");

        var request = new CreateIcsTokenMobileRequest { Label = label };
        var result = await _apiClient.CreateIcsTokenAsync(request);
        if (result.Success)
            await LoadCalendarDataAsync();
        else
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create token", "OK");
    }

    #endregion

    #region Helpers

    private static BoxView CreateDivider() => new()
    {
        HeightRequest = 1,
        BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#424242")
            : Color.FromArgb("#E0E0E0"),
        Margin = new Thickness(0, 10)
    };

    private ICalendarSyncService? GetCalendarSyncService()
    {
        return Handler?.MauiContext?.Services.GetService<ICalendarSyncService>();
    }

    #endregion
}
