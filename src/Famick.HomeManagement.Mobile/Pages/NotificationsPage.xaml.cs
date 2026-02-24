using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class NotificationsPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    private readonly ObservableCollection<NotificationDisplayItem> _notifications = [];
    private int _currentPage = 1;
    private int _totalCount;
    private bool _isLoadingMore;
    private bool _showUnreadOnly;
    private const int PageSize = 20;

    public NotificationsPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        NotificationsList.BindingContext = _notifications;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _currentPage = 1;
        await LoadNotificationsAsync(reset: true);
    }

    private async Task LoadNotificationsAsync(bool reset = false)
    {
        if (reset)
        {
            _currentPage = 1;
            ShowLoading(true);
        }

        ErrorAlert.IsVisible = false;

        try
        {
            bool? readFilter = _showUnreadOnly ? false : null;
            var result = await _apiClient.GetNotificationsAsync(_currentPage, PageSize, readFilter);

            if (result.Success && result.Data != null)
            {
                _totalCount = result.Data.TotalCount;

                if (reset)
                {
                    _notifications.Clear();
                }

                foreach (var dto in result.Data.Items)
                {
                    _notifications.Add(NotificationDisplayItem.FromDto(dto));
                }

                UpdateUI();
            }
            else
            {
                if (reset)
                {
                    ShowError(result.ErrorMessage ?? "Failed to load notifications");
                }
            }
        }
        catch (Exception ex)
        {
            if (reset)
            {
                ShowError($"Failed to load: {ex.Message}");
            }
        }
        finally
        {
            ShowLoading(false);
            _isLoadingMore = false;
        }
    }

    private void UpdateUI()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var hasItems = _notifications.Count > 0;
            EmptyState.IsVisible = !hasItems;
            MarkAllReadButton.IsVisible = hasItems && _notifications.Any(n => n.IsUnread);
        });
    }

    private void ShowLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;
        });
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.Text = message;
            ErrorAlert.IsVisible = true;
        });
    }

    private void UpdateTabStyles()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_showUnreadOnly)
            {
                AllTab.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");
                AllTab.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#999999") : Color.FromArgb("#666666");
                UnreadTab.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#1565C0") : Color.FromArgb("#1976D2");
                UnreadTab.TextColor = Colors.White;
            }
            else
            {
                AllTab.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#1565C0") : Color.FromArgb("#1976D2");
                AllTab.TextColor = Colors.White;
                UnreadTab.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");
                UnreadTab.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#999999") : Color.FromArgb("#666666");
            }
        });
    }

    private async void OnAllTabClicked(object? sender, EventArgs e)
    {
        if (!_showUnreadOnly) return;
        _showUnreadOnly = false;
        UpdateTabStyles();
        await LoadNotificationsAsync(reset: true);
    }

    private async void OnUnreadTabClicked(object? sender, EventArgs e)
    {
        if (_showUnreadOnly) return;
        _showUnreadOnly = true;
        UpdateTabStyles();
        await LoadNotificationsAsync(reset: true);
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadNotificationsAsync(reset: true);
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnLoadMore(object? sender, EventArgs e)
    {
        if (_isLoadingMore || _notifications.Count >= _totalCount) return;

        _isLoadingMore = true;
        _currentPage++;
        await LoadNotificationsAsync(reset: false);
    }

    private async void OnNotificationTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not NotificationDisplayItem item) return;

        // Mark as read
        if (item.IsUnread)
        {
            var result = await _apiClient.MarkNotificationReadAsync(item.Id);
            if (result.Success)
            {
                item.MarkAsRead();
                UpdateUI();
            }
        }

        // Navigate to deep link if available
        if (!string.IsNullOrEmpty(item.DeepLinkUrl))
        {
            try
            {
                await Shell.Current.GoToAsync(item.DeepLinkUrl);
            }
            catch
            {
                // Deep link may not be a valid shell route - ignore
            }
        }
    }

    private async void OnSwipeDismiss(object? sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: NotificationDisplayItem item }) return;

        var result = await _apiClient.DismissNotificationAsync(item.Id);
        if (result.Success)
        {
            _notifications.Remove(item);
            _totalCount--;
            UpdateUI();
        }
    }

    private async void OnMarkAllReadClicked(object? sender, EventArgs e)
    {
        var result = await _apiClient.MarkAllNotificationsReadAsync();
        if (result.Success)
        {
            foreach (var n in _notifications)
            {
                n.MarkAsRead();
            }
            UpdateUI();
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadNotificationsAsync(reset: true);
    }
}

/// <summary>
/// Display model for a notification item in the CollectionView.
/// </summary>
public class NotificationDisplayItem : BindableObject
{
    public Guid Id { get; init; }
    public int Type { get; init; }
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string? DeepLinkUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsUnread { get; private set; }

    public string TypeIcon => Type switch
    {
        1 => "\u26A0\uFE0F",      // ExpiryLowStock - Warning
        2 => "\u2705",             // TaskSummary - Check mark
        3 => "\U0001F389",         // NewFeatures - Party popper
        _ => "\U0001F514"          // Default - Bell
    };

    public string TitleFontAttributes => IsUnread ? "Bold" : "None";

    public Color BackgroundColor => IsUnread
        ? (Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#1A237E")
            : Color.FromArgb("#E3F2FD"))
        : (Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2A2A2A")
            : Colors.White);

    public string TimeAgo
    {
        get
        {
            var elapsed = DateTime.UtcNow - CreatedAt;
            return elapsed.TotalMinutes switch
            {
                < 1 => "Just now",
                < 60 => $"{(int)elapsed.TotalMinutes}m ago",
                < 1440 => $"{(int)elapsed.TotalHours}h ago",
                _ => $"{(int)elapsed.TotalDays}d ago"
            };
        }
    }

    public void MarkAsRead()
    {
        IsUnread = false;
        OnPropertyChanged(nameof(IsUnread));
        OnPropertyChanged(nameof(TitleFontAttributes));
        OnPropertyChanged(nameof(BackgroundColor));
    }

    public static NotificationDisplayItem FromDto(NotificationItemDto dto) => new()
    {
        Id = dto.Id,
        Type = dto.Type,
        Title = dto.Title,
        Summary = dto.Summary,
        DeepLinkUrl = dto.DeepLinkUrl,
        CreatedAt = dto.CreatedAt,
        IsUnread = !dto.IsRead
    };
}
