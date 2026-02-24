using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Calendar;

[QueryProperty(nameof(EventId), "EventId")]
[QueryProperty(nameof(OriginalStartTimeUtc), "OriginalStartTimeUtc")]
public partial class CalendarEventDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private CalendarEventDetail? _event;

    public string EventId { get; set; } = string.Empty;
    public string OriginalStartTimeUtc { get; set; } = string.Empty;

    public CalendarEventDetailPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadEventAsync();
    }

    private async Task LoadEventAsync()
    {
        if (!Guid.TryParse(EventId, out var id))
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError("Invalid event ID"));
            return;
        }

        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var result = await _apiClient.GetCalendarEventAsync(id);
            if (result.Success && result.Data != null)
            {
                _event = result.Data;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RenderEvent();
                    ShowContent();
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError(result.ErrorMessage ?? "Failed to load event"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                ShowError($"Connection error: {ex.Message}"));
        }
    }

    private void RenderEvent()
    {
        if (_event == null) return;

        TitleLabel.Text = _event.Title;

        // Date
        var startLocal = _event.StartTimeUtc.ToLocalTime();
        var endLocal = _event.EndTimeUtc.ToLocalTime();

        if (_event.IsAllDay)
        {
            DateLabel.Text = startLocal.ToString("dddd, MMMM d, yyyy");
            TimeRow.IsVisible = false;
        }
        else if (startLocal.Date == endLocal.Date)
        {
            DateLabel.Text = startLocal.ToString("dddd, MMMM d, yyyy");
            TimeLabel.Text = $"{startLocal:h:mm tt} – {endLocal:h:mm tt}";
        }
        else
        {
            DateLabel.Text = $"{startLocal:MMM d} – {endLocal:MMM d, yyyy}";
            TimeLabel.Text = $"{startLocal:h:mm tt} – {endLocal:h:mm tt}";
        }

        // Location
        if (!string.IsNullOrWhiteSpace(_event.Location))
        {
            LocationLabel.Text = _event.Location;
            LocationSection.IsVisible = true;
        }

        // Description
        if (!string.IsNullOrWhiteSpace(_event.Description))
        {
            DescriptionLabel.Text = _event.Description;
            DescriptionSection.IsVisible = true;
        }

        // Recurrence
        if (!string.IsNullOrEmpty(_event.RecurrenceRule))
        {
            RecurrenceLabel.Text = FormatRecurrenceRule(_event.RecurrenceRule);
            RecurrenceSection.IsVisible = true;
        }

        // Reminder
        if (_event.ReminderMinutesBefore.HasValue)
        {
            ReminderLabel.Text = FormatReminder(_event.ReminderMinutesBefore.Value);
            ReminderSection.IsVisible = true;
        }

        // Members
        if (_event.Members.Count > 0)
        {
            MembersHeader.IsVisible = true;
            MembersList.Children.Clear();
            foreach (var member in _event.Members)
            {
                var isInvolved = member.ParticipationType == 1;
                var card = new Border
                {
                    Padding = new Thickness(12, 8),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    Stroke = Colors.Transparent,
                    BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5")
                };
                var row = new HorizontalStackLayout { Spacing = 8 };
                row.Children.Add(new Label
                {
                    Text = member.UserDisplayName,
                    FontSize = 14,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Colors.White : Colors.Black
                });
                var typeBadge = new Border
                {
                    Padding = new Thickness(6, 2),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    Stroke = Colors.Transparent,
                    BackgroundColor = isInvolved
                        ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#EEEEEE"),
                    Content = new Label
                    {
                        Text = isInvolved ? "Involved" : "Aware",
                        FontSize = 11,
                        TextColor = isInvolved
                            ? Color.FromArgb("#2E7D32") : Color.FromArgb("#757575")
                    }
                };
                row.Children.Add(typeBadge);
                card.Content = row;
                MembersList.Children.Add(card);
            }
        }

        // Created by
        if (!string.IsNullOrEmpty(_event.CreatedByUserName))
        {
            CreatedByLabel.Text = $"Created by {_event.CreatedByUserName} on {_event.CreatedAt.ToLocalTime():MMM d, yyyy}";
        }
    }

    private static string FormatRecurrenceRule(string rrule)
    {
        if (rrule.Contains("FREQ=DAILY")) return "Repeats daily";
        if (rrule.Contains("FREQ=WEEKLY") && rrule.Contains("INTERVAL=2")) return "Repeats every 2 weeks";
        if (rrule.Contains("FREQ=WEEKLY")) return "Repeats weekly";
        if (rrule.Contains("FREQ=MONTHLY")) return "Repeats monthly";
        if (rrule.Contains("FREQ=YEARLY")) return "Repeats yearly";
        return "Repeats (custom)";
    }

    private static string FormatReminder(int minutes)
    {
        if (minutes < 60) return $"{minutes} minutes before";
        if (minutes == 60) return "1 hour before";
        if (minutes < 1440) return $"{minutes / 60} hours before";
        if (minutes == 1440) return "1 day before";
        return $"{minutes / 1440} days before";
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_event == null) return;

        if (!string.IsNullOrEmpty(_event.RecurrenceRule))
        {
            var scope = await ShowEditScopeActionSheet("Edit");
            if (scope == null) return;

            await Shell.Current.GoToAsync(nameof(CreateEditEventPage), new Dictionary<string, object>
            {
                { "EventId", _event.Id.ToString() },
                { "EditScope", scope.Value.ToString() },
                { "OccurrenceStartTimeUtc", OriginalStartTimeUtc }
            });
        }
        else
        {
            await Shell.Current.GoToAsync(nameof(CreateEditEventPage), new Dictionary<string, object>
            {
                { "EventId", _event.Id.ToString() }
            });
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_event == null) return;

        if (!string.IsNullOrEmpty(_event.RecurrenceRule))
        {
            var scope = await ShowEditScopeActionSheet("Delete");
            if (scope == null) return;

            var confirm = await DisplayAlertAsync("Delete Event",
                "Are you sure you want to delete this event?", "Delete", "Cancel");
            if (!confirm) return;

            DateTime? occStart = null;
            if (!string.IsNullOrEmpty(OriginalStartTimeUtc) &&
                DateTime.TryParse(OriginalStartTimeUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                occStart = parsed;
            }

            var result = await _apiClient.DeleteCalendarEventAsync(_event.Id,
                new DeleteCalendarEventMobileRequest
                {
                    EditScope = scope.Value,
                    OccurrenceStartTimeUtc = occStart
                });

            if (result.Success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to delete event", "OK");
            }
        }
        else
        {
            var confirm = await DisplayAlertAsync("Delete Event",
                $"Are you sure you want to delete \"{_event.Title}\"?", "Delete", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.DeleteCalendarEventAsync(_event.Id);
            if (result.Success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to delete event", "OK");
            }
        }
    }

    private async Task<int?> ShowEditScopeActionSheet(string action)
    {
        var result = await DisplayActionSheetAsync(
            $"{action} Recurring Event",
            "Cancel",
            null,
            "This Occurrence Only",
            "This and Future Events",
            "Entire Series");

        return result switch
        {
            "This Occurrence Only" => 1,
            "This and Future Events" => 2,
            "Entire Series" => 3,
            _ => null
        };
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadEventAsync();
    }

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;
        ErrorFrame.IsVisible = false;
    }

    private void ShowContent()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = true;
        ErrorFrame.IsVisible = false;
    }

    private void ShowError(string message)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = false;
        ErrorFrame.IsVisible = true;
        ErrorLabel.Text = message;
    }
}
