using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Calendar;

public partial class CalendarPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private DateTime _rangeStart;
    private DateTime _rangeEnd;
    private List<CalendarOccurrence> _events = new();

    public CalendarPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SetWeekRange(DateTime.Today);
        await LoadEventsAsync();
    }

    private void SetWeekRange(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var monday = date.AddDays(-(dayOfWeek == 0 ? 6 : dayOfWeek - 1));
        _rangeStart = monday;
        _rangeEnd = monday.AddDays(6);
        UpdateDateRangeLabel();
    }

    private void UpdateDateRangeLabel()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DateRangeLabel.Text = $"{_rangeStart:MMM d} – {_rangeEnd:MMM d, yyyy}";
        });
    }

    private async Task LoadEventsAsync()
    {
        ShowLoading();

        try
        {
            var result = await _apiClient.GetCalendarEventsAsync(_rangeStart, _rangeEnd.AddDays(1));
            if (result.Success && result.Data != null)
            {
                _events = result.Data.OrderBy(e => e.StartTimeUtc).ToList();
                MainThread.BeginInvokeOnMainThread(RenderAgendaView);
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError(result.ErrorMessage ?? "Failed to load events"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                ShowError($"Connection error: {ex.Message}"));
        }
    }

    private void RenderAgendaView()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ErrorFrame.IsVisible = false;
        ContentGrid.IsVisible = true;

        // Clear the collection view and build agenda manually
        var agendaLayout = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(12, 0) };

        if (_events.Count == 0)
        {
            EmptyState.IsVisible = true;
            // Replace collection view content with empty layout
            RefreshContainer.Content = new ScrollView { Content = agendaLayout };
            return;
        }

        EmptyState.IsVisible = false;

        // Group by local date
        var grouped = _events.GroupBy(e => e.StartTimeLocal.Date).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            // Date header
            var dateLabel = FormatDateLabel(group.Key);
            var dayLabel = group.Key.ToString("dddd");

            var headerLayout = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(0, 12, 0, 4) };
            headerLayout.Children.Add(new Label
            {
                Text = dateLabel,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = group.Key == DateTime.Today
                    ? Color.FromArgb("#518751")
                    : (Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Colors.White : Colors.Black)
            });
            headerLayout.Children.Add(new Label
            {
                Text = dayLabel,
                FontSize = 13,
                TextColor = Colors.Gray,
                VerticalOptions = LayoutOptions.End
            });
            agendaLayout.Children.Add(headerLayout);

            // Event cards
            foreach (var evt in group)
            {
                agendaLayout.Children.Add(CreateEventCard(evt));
            }
        }

        RefreshContainer.Content = new ScrollView { Content = agendaLayout };
    }

    private View CreateEventCard(CalendarOccurrence evt)
    {
        var eventColor = GetEventColor(evt);

        var card = new Border
        {
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 2),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = Colors.Transparent,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2A2A2A") : Colors.White
        };
        card.Shadow = new Shadow
        {
            Brush = new SolidColorBrush(Color.FromArgb("#20000000")),
            Offset = new Point(0, 1),
            Radius = 4,
            Opacity = 0.15f
        };

        var mainGrid = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(new GridLength(55)),
                new ColumnDefinition(new GridLength(4)),
                new ColumnDefinition(GridLength.Star)
            }
        };

        // Time column
        var timeLayout = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
        if (evt.IsAllDay)
        {
            timeLayout.Children.Add(new Label
            {
                Text = "All Day",
                FontSize = 12,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.Center
            });
        }
        else
        {
            timeLayout.Children.Add(new Label
            {
                Text = evt.StartTimeLocal.ToString("h:mm"),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.White : Colors.Black
            });
            timeLayout.Children.Add(new Label
            {
                Text = evt.StartTimeLocal.ToString("tt").ToLower(),
                FontSize = 11,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.Center
            });
        }
        mainGrid.Children.Add(timeLayout);

        // Color bar
        var colorBar = new BoxView
        {
            Color = SafeParseColor(eventColor),
            WidthRequest = 4,
            CornerRadius = 2,
            VerticalOptions = LayoutOptions.Fill
        };
        Grid.SetColumn(colorBar, 1);
        mainGrid.Children.Add(colorBar);

        // Event details
        var detailLayout = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };

        var titleLayout = new HorizontalStackLayout { Spacing = 6 };
        titleLayout.Children.Add(new Label
        {
            Text = evt.Title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Colors.White : Colors.Black
        });
        if (evt.IsExternal)
        {
            var externalBadge = new Border
            {
                Padding = new Thickness(6, 1),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Gray,
                StrokeThickness = 1,
                BackgroundColor = Colors.Transparent,
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = "External",
                    FontSize = 10,
                    TextColor = Colors.Gray
                }
            };
            titleLayout.Children.Add(externalBadge);
        }
        detailLayout.Children.Add(titleLayout);

        // Time range
        if (!evt.IsAllDay)
        {
            detailLayout.Children.Add(new Label
            {
                Text = $"{evt.StartTimeLocal:h:mm tt} – {evt.EndTimeLocal:h:mm tt}",
                FontSize = 12,
                TextColor = Colors.Gray
            });
        }

        // Location
        if (!string.IsNullOrWhiteSpace(evt.Location))
        {
            var locationLayout = new HorizontalStackLayout { Spacing = 4 };
            locationLayout.Children.Add(new Label
            {
                Text = "\uD83D\uDCCD",
                FontSize = 11
            });
            locationLayout.Children.Add(new Label
            {
                Text = evt.Location,
                FontSize = 12,
                TextColor = Colors.Gray,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            });
            detailLayout.Children.Add(locationLayout);
        }

        // Members
        if (evt.Members.Count > 0)
        {
            var membersLayout = new HorizontalStackLayout { Spacing = 6, Margin = new Thickness(0, 2, 0, 0) };
            foreach (var member in evt.Members.Take(3))
            {
                var isInvolved = member.ParticipationType == 1;
                var chip = new Border
                {
                    Padding = new Thickness(6, 2),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Stroke = Colors.Transparent,
                    BackgroundColor = isInvolved
                        ? Color.FromArgb("#E8F5E9")
                        : Color.FromArgb("#F5F5F5"),
                    Content = new Label
                    {
                        Text = member.UserDisplayName,
                        FontSize = 11,
                        TextColor = isInvolved
                            ? Color.FromArgb("#2E7D32")
                            : Color.FromArgb("#757575")
                    }
                };
                membersLayout.Children.Add(chip);
            }
            if (evt.Members.Count > 3)
            {
                membersLayout.Children.Add(new Label
                {
                    Text = $"+{evt.Members.Count - 3}",
                    FontSize = 11,
                    TextColor = Colors.Gray,
                    VerticalOptions = LayoutOptions.Center
                });
            }
            detailLayout.Children.Add(membersLayout);
        }

        Grid.SetColumn(detailLayout, 2);
        mainGrid.Children.Add(detailLayout);

        card.Content = mainGrid;

        // Tap to view event detail
        var tapGesture = new TapGestureRecognizer();
        var eventId = evt.EventId;
        var isExternal = evt.IsExternal;
        var originalStart = evt.OriginalStartTimeUtc;
        tapGesture.Tapped += async (_, _) =>
        {
            if (isExternal) return;
            await Shell.Current.GoToAsync(nameof(CalendarEventDetailPage), new Dictionary<string, object>
            {
                { "EventId", eventId.ToString() },
                { "OriginalStartTimeUtc", originalStart?.ToString("O") ?? "" }
            });
        };
        card.GestureRecognizers.Add(tapGesture);

        return card;
    }

    private static string GetEventColor(CalendarOccurrence evt)
    {
        if (!string.IsNullOrEmpty(evt.Color)) return evt.Color;
        return evt.IsExternal ? "#9E9E9E" : "#518751";
    }

    private static Color SafeParseColor(string colorValue)
    {
        if (string.IsNullOrEmpty(colorValue))
            return Color.FromArgb("#518751");

        // Hex color
        if (colorValue[0] == '#')
        {
            try { return Color.FromArgb(colorValue); }
            catch { return Color.FromArgb("#518751"); }
        }

        // Named CSS color
        return colorValue.ToLowerInvariant() switch
        {
            "red" => Color.FromArgb("#F44336"),
            "blue" => Color.FromArgb("#2196F3"),
            "green" => Color.FromArgb("#4CAF50"),
            "yellow" => Color.FromArgb("#FFEB3B"),
            "orange" => Color.FromArgb("#FF9800"),
            "purple" => Color.FromArgb("#9C27B0"),
            "pink" => Color.FromArgb("#E91E63"),
            "teal" => Color.FromArgb("#009688"),
            "cyan" => Color.FromArgb("#00BCD4"),
            "brown" => Color.FromArgb("#795548"),
            "gray" or "grey" => Color.FromArgb("#9E9E9E"),
            "indigo" => Color.FromArgb("#3F51B5"),
            "amber" => Color.FromArgb("#FFC107"),
            "black" => Color.FromArgb("#000000"),
            "white" => Color.FromArgb("#FFFFFF"),
            _ => Color.FromArgb("#518751") // fallback green
        };
    }

    private static string FormatDateLabel(DateTime date)
    {
        var today = DateTime.Today;
        if (date == today) return "Today";
        if (date == today.AddDays(1)) return "Tomorrow";
        if (date == today.AddDays(-1)) return "Yesterday";
        return date.ToString("MMM d");
    }

    private async void OnPreviousWeekClicked(object? sender, EventArgs e)
    {
        _rangeStart = _rangeStart.AddDays(-7);
        _rangeEnd = _rangeEnd.AddDays(-7);
        UpdateDateRangeLabel();
        await LoadEventsAsync();
    }

    private async void OnNextWeekClicked(object? sender, EventArgs e)
    {
        _rangeStart = _rangeStart.AddDays(7);
        _rangeEnd = _rangeEnd.AddDays(7);
        UpdateDateRangeLabel();
        await LoadEventsAsync();
    }

    private async void OnTodayClicked(object? sender, EventArgs e)
    {
        SetWeekRange(DateTime.Today);
        await LoadEventsAsync();
    }

    private async void OnAddEventClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CreateEditEventPage));
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadEventsAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadEventsAsync();
    }

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ErrorFrame.IsVisible = false;
        ContentGrid.IsVisible = false;
    }

    private void ShowError(string message)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ErrorFrame.IsVisible = true;
        ErrorLabel.Text = message;
        ContentGrid.IsVisible = false;
    }
}
