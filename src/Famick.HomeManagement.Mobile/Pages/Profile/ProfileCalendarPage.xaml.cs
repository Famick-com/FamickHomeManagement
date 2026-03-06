using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using static Famick.HomeManagement.Mobile.Pages.Profile.ProfileUiHelpers;

namespace Famick.HomeManagement.Mobile.Pages.Profile;

public partial class ProfileCalendarPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private List<ExternalCalendarSubscriptionMobile>? _subscriptions;
    private List<IcsTokenMobile>? _icsTokens;
    private bool _loaded;

    public ProfileCalendarPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
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
            await Task.WhenAll(subsTask, tokensTask);

            if (subsTask.Result.Success)
                _subscriptions = subsTask.Result.Data;
            if (tokensTask.Result.Success)
                _icsTokens = tokensTask.Result.Data;

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

        // External Subscriptions section
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

        // Divider
        CalendarStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#424242")
                : Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 10)
        });

        // ICS Feed Tokens section
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
}
