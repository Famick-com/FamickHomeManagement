using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using static Famick.HomeManagement.Mobile.Pages.Profile.ProfileUiHelpers;

namespace Famick.HomeManagement.Mobile.Pages.Profile;

public partial class ProfileContactSyncPage : ContentPage
{
    private readonly ContactSyncOrchestrator _orchestrator;
    private ContactSyncStatus? _status;
    private bool _loaded;
    private bool _isSyncing;

    public ProfileContactSyncPage(ContactSyncOrchestrator orchestrator)
    {
        InitializeComponent();
        _orchestrator = orchestrator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded)
        {
            await LoadStatusAsync();
            _loaded = true;
        }
    }

    private async Task LoadStatusAsync()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            _status = await _orchestrator.GetStatusAsync();
            RenderContent();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load sync status: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void RenderContent()
    {
        SyncStack.Children.Clear();

        // Title
        SyncStack.Children.Add(CreateLabel("Contact Sync", true, 18));
        SyncStack.Children.Add(CreateLabel(
            "Sync your Famick contacts to this device's Contacts app under a \"Famick\" group.", false, 14));

        // Permission status card
        var permissionStatus = _status?.HasPermission == true ? "Granted" : "Not Granted";
        var permissionColor = _status?.HasPermission == true
            ? Color.FromArgb("#4CAF50")
            : Color.FromArgb("#FF9800");

        var permissionContent = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                CreateLabel("Permission", true),
                new Label
                {
                    Text = $"Contact access: {permissionStatus}",
                    FontSize = 14,
                    TextColor = permissionColor
                }
            }
        };

        if (_status?.HasPermission != true)
        {
            var requestBtn = new Button
            {
                Text = "Grant Permission",
                BackgroundColor = Color.FromArgb("#1976D2"),
                TextColor = Colors.White,
                CornerRadius = 6,
                FontSize = 14,
                Padding = new Thickness(15, 8)
            };
            requestBtn.Clicked += OnRequestPermissionClicked;
            permissionContent.Children.Add(requestBtn);
        }

        SyncStack.Children.Add(CreateCard(permissionContent));

        // Sync toggle card
        var syncEnabled = ContactSyncOrchestrator.IsSyncEnabled;
        var toggleSwitch = new Switch
        {
            IsToggled = syncEnabled,
            OnColor = Color.FromArgb("#1976D2")
        };
        toggleSwitch.Toggled += OnSyncToggled;

        SyncStack.Children.Add(CreateCard(new HorizontalStackLayout
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
                        CreateLabel("Enable Contact Sync", true),
                        CreateLabel("Automatically sync contacts to this device", false, 12)
                    }
                },
                toggleSwitch
            }
        }));

        // Status card
        var lastSynced = _status?.LastSyncedAt?.ToLocalTime().ToString("g") ?? "Never";
        var syncedCount = _status?.SyncedCount ?? 0;

        SyncStack.Children.Add(CreateCard(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                CreateLabel("Sync Status", true),
                CreateLabel($"Synced contacts: {syncedCount}", false, 14),
                CreateLabel($"Last synced: {lastSynced}", false, 14)
            }
        }));

        // Sync Now button
        var syncNowBtn = new Button
        {
            Text = _isSyncing ? "Syncing..." : "Sync Now",
            BackgroundColor = Color.FromArgb("#1976D2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 10),
            IsEnabled = !_isSyncing && _status?.HasPermission == true
        };
        syncNowBtn.Clicked += OnSyncNowClicked;
        SyncStack.Children.Add(syncNowBtn);

        // Remove All button
        if (syncedCount > 0)
        {
            var removeBtn = new Button
            {
                Text = "Remove All Synced Contacts",
                BackgroundColor = Color.FromArgb("#D32F2F"),
                TextColor = Colors.White,
                CornerRadius = 8,
                Padding = new Thickness(20, 10),
                IsEnabled = !_isSyncing
            };
            removeBtn.Clicked += OnRemoveAllClicked;
            SyncStack.Children.Add(removeBtn);
        }

        // Spacer at bottom
        SyncStack.Children.Add(new BoxView { HeightRequest = 20, BackgroundColor = Colors.Transparent });
    }

    private async void OnRequestPermissionClicked(object? sender, EventArgs e)
    {
        var syncService = GetSyncService();
        if (syncService != null)
        {
            await syncService.RequestPermissionAsync();
            _loaded = false;
            await LoadStatusAsync();
        }
    }

    private void OnSyncToggled(object? sender, ToggledEventArgs e)
    {
        ContactSyncOrchestrator.IsSyncEnabled = e.Value;

        if (e.Value)
        {
#if IOS
            Platforms.iOS.BackgroundContactSyncTask.ScheduleNextSync();
#elif ANDROID
            Platforms.Android.ContactSyncWorker.Schedule();
#endif
        }
        else
        {
#if IOS
            Platforms.iOS.BackgroundContactSyncTask.CancelScheduledSync();
#elif ANDROID
            Platforms.Android.ContactSyncWorker.Cancel();
#endif
        }
    }

    private async void OnSyncNowClicked(object? sender, EventArgs e)
    {
        _isSyncing = true;
        RenderContent();

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
            await LoadStatusAsync();
        }
    }

    private async void OnRemoveAllClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Remove All",
            "This will remove all Famick-synced contacts from your device's Contacts app. This cannot be undone.",
            "Remove All",
            "Cancel");

        if (!confirm) return;

        _isSyncing = true;
        RenderContent();

        try
        {
            var result = await _orchestrator.RemoveAllAsync();
            if (result.Success)
            {
                await DisplayAlert("Done", $"Removed {result.Deleted} contacts from device", "OK");
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to remove contacts", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Remove failed: {ex.Message}", "OK");
        }
        finally
        {
            _isSyncing = false;
            _loaded = false;
            await LoadStatusAsync();
        }
    }

    private IContactSyncService? GetSyncService()
    {
        return Handler?.MauiContext?.Services.GetService<IContactSyncService>();
    }
}
