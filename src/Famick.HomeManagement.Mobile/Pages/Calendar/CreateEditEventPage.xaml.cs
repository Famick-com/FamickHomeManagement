using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Calendar;

[QueryProperty(nameof(EventId), "EventId")]
[QueryProperty(nameof(EditScope), "EditScope")]
[QueryProperty(nameof(OccurrenceStartTimeUtc), "OccurrenceStartTimeUtc")]
public partial class CreateEditEventPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private CalendarEventDetail? _existingEvent;
    private List<HouseholdMember> _allMembers = new();
    private readonly List<CalendarMemberRequest> _selectedMembers = new();
    private bool _isEditMode;

    public string EventId { get; set; } = string.Empty;
    public string EditScope { get; set; } = string.Empty;
    public string OccurrenceStartTimeUtc { get; set; } = string.Empty;

    public CreateEditEventPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;

        // Set defaults
        RecurrencePicker.SelectedIndex = 0;
        ReminderPicker.SelectedIndex = 0;
        StartDatePicker.Date = DateTime.Today;
        EndDatePicker.Date = DateTime.Today;
        var defaultStart = RoundToNext15(DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(1)));
        StartTimePicker.Time = defaultStart;
        EndTimePicker.Time = defaultStart.Add(TimeSpan.FromHours(1));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Load household members for picker
        var membersResult = await _apiClient.GetCalendarMembersAsync();
        if (membersResult.Success && membersResult.Data != null)
        {
            _allMembers = membersResult.Data;
        }

        // If EventId is set, load existing event for editing
        if (Guid.TryParse(EventId, out var id))
        {
            _isEditMode = true;
            Title = "Edit Event";
            ShowLoading(true);

            var result = await _apiClient.GetCalendarEventAsync(id);
            if (result.Success && result.Data != null)
            {
                _existingEvent = result.Data;
                PopulateForm();
            }

            ShowLoading(false);
        }
        else
        {
            // Auto-add the current user as Involved when creating a new event
            var currentUser = _allMembers.FirstOrDefault(m => m.IsCurrentUser);
            if (currentUser != null)
            {
                _selectedMembers.Add(new CalendarMemberRequest
                {
                    UserId = currentUser.Id,
                    ParticipationType = 1 // Involved
                });
                RenderMembers();
            }
        }
    }

    private void PopulateForm()
    {
        if (_existingEvent == null) return;

        TitleEntry.Text = _existingEvent.Title;
        LocationEntry.Text = _existingEvent.Location;
        DescriptionEditor.Text = _existingEvent.Description;
        AllDaySwitch.IsToggled = _existingEvent.IsAllDay;

        var startLocal = _existingEvent.StartTimeUtc.ToLocalTime();
        var endLocal = _existingEvent.EndTimeUtc.ToLocalTime();
        StartDatePicker.Date = startLocal.Date;
        StartTimePicker.Time = startLocal.TimeOfDay;
        EndDatePicker.Date = endLocal.Date;
        EndTimePicker.Time = endLocal.TimeOfDay;

        // Recurrence
        RecurrencePicker.SelectedIndex = GetRecurrenceIndex(_existingEvent.RecurrenceRule);

        // Reminder
        ReminderPicker.SelectedIndex = GetReminderIndex(_existingEvent.ReminderMinutesBefore);

        // Members
        _selectedMembers.Clear();
        foreach (var member in _existingEvent.Members)
        {
            _selectedMembers.Add(new CalendarMemberRequest
            {
                UserId = member.UserId,
                ParticipationType = member.ParticipationType
            });
        }
        RenderMembers();
    }

    private void OnAllDayToggled(object? sender, ToggledEventArgs e)
    {
        StartTimeSection.IsVisible = !e.Value;
        EndTimeSection.IsVisible = !e.Value;
    }

    private async void OnAddMemberClicked(object? sender, EventArgs e)
    {
        // Filter out already-added members
        var available = _allMembers
            .Where(m => _selectedMembers.All(s => s.UserId != m.Id))
            .ToList();

        if (available.Count == 0)
        {
            await DisplayAlertAsync("No More Members", "All household members have been added.", "OK");
            return;
        }

        var names = available.Select(m => m.DisplayName).ToArray();
        var selected = await DisplayActionSheetAsync("Add Member", "Cancel", null, names);

        if (string.IsNullOrEmpty(selected) || selected == "Cancel") return;

        var member = available.FirstOrDefault(m => m.DisplayName == selected);
        if (member == null) return;

        // Ask participation type
        var type = await DisplayActionSheetAsync("Participation", "Cancel", null, "Involved", "Aware");
        if (string.IsNullOrEmpty(type) || type == "Cancel") return;

        _selectedMembers.Add(new CalendarMemberRequest
        {
            UserId = member.Id,
            ParticipationType = type == "Involved" ? 1 : 2
        });
        RenderMembers();
    }

    private void RenderMembers()
    {
        MembersContainer.Children.Clear();
        foreach (var member in _selectedMembers)
        {
            var memberInfo = _allMembers.FirstOrDefault(m => m.Id == member.UserId);
            var displayName = memberInfo?.DisplayName ?? "Unknown";
            var isInvolved = member.ParticipationType == 1;

            var card = new Border
            {
                Padding = new Thickness(12, 8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5")
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8
            };

            grid.Children.Add(new Label
            {
                Text = displayName,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
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
            Grid.SetColumn(typeBadge, 1);
            grid.Children.Add(typeBadge);

            var removeBtn = new Button
            {
                Text = "\u2715",
                FontSize = 14,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#E53935"),
                WidthRequest = 32,
                HeightRequest = 32,
                Padding = 0
            };
            var userId = member.UserId;
            removeBtn.Clicked += (_, _) =>
            {
                _selectedMembers.RemoveAll(m => m.UserId == userId);
                RenderMembers();
            };
            Grid.SetColumn(removeBtn, 2);
            grid.Children.Add(removeBtn);

            card.Content = grid;
            MembersContainer.Children.Add(card);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var title = TitleEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            await DisplayAlertAsync("Validation", "Title is required.", "OK");
            return;
        }

        var isAllDay = AllDaySwitch.IsToggled;
        var startDate = StartDatePicker.Date ?? DateTime.Today;
        var endDate = EndDatePicker.Date ?? DateTime.Today;
        var startTime = StartTimePicker.Time ?? TimeSpan.Zero;
        var endTime = EndTimePicker.Time ?? TimeSpan.FromHours(1);

        DateTime startUtc, endUtc;
        if (isAllDay)
        {
            startUtc = startDate.Date.ToUniversalTime();
            endUtc = endDate.Date.AddDays(1).ToUniversalTime();
        }
        else
        {
            startUtc = (startDate + startTime).ToUniversalTime();
            endUtc = (endDate + endTime).ToUniversalTime();
        }

        if (endUtc <= startUtc)
        {
            await DisplayAlertAsync("Validation", "End time must be after start time.", "OK");
            return;
        }

        var recurrenceRule = GetRecurrenceRule();
        var reminderMinutes = GetReminderMinutes();

        ShowLoading(true);

        try
        {
            if (_isEditMode && _existingEvent != null)
            {
                int? editScope = null;
                if (int.TryParse(EditScope, out var parsed))
                    editScope = parsed;

                DateTime? occStart = null;
                if (!string.IsNullOrEmpty(OccurrenceStartTimeUtc) &&
                    DateTime.TryParse(OccurrenceStartTimeUtc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var occParsed))
                {
                    occStart = occParsed;
                }

                var request = new UpdateCalendarEventMobileRequest
                {
                    Title = title,
                    Description = DescriptionEditor.Text?.Trim(),
                    Location = LocationEntry.Text?.Trim(),
                    StartTimeUtc = startUtc,
                    EndTimeUtc = endUtc,
                    IsAllDay = isAllDay,
                    RecurrenceRule = recurrenceRule,
                    ReminderMinutesBefore = reminderMinutes,
                    Members = _selectedMembers,
                    EditScope = editScope,
                    OccurrenceStartTimeUtc = occStart
                };

                var result = await _apiClient.UpdateCalendarEventAsync(_existingEvent.Id, request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to update event", "OK");
                }
            }
            else
            {
                var request = new CreateCalendarEventMobileRequest
                {
                    Title = title,
                    Description = DescriptionEditor.Text?.Trim(),
                    Location = LocationEntry.Text?.Trim(),
                    StartTimeUtc = startUtc,
                    EndTimeUtc = endUtc,
                    IsAllDay = isAllDay,
                    RecurrenceRule = recurrenceRule,
                    ReminderMinutesBefore = reminderMinutes,
                    Members = _selectedMembers
                };

                var result = await _apiClient.CreateCalendarEventAsync(request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to create event", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed: {ex.Message}", "OK");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private string? GetRecurrenceRule()
    {
        return RecurrencePicker.SelectedIndex switch
        {
            1 => "FREQ=DAILY",
            2 => "FREQ=WEEKLY",
            3 => "FREQ=WEEKLY;INTERVAL=2",
            4 => "FREQ=MONTHLY",
            5 => "FREQ=YEARLY",
            _ => null
        };
    }

    private int? GetReminderMinutes()
    {
        return ReminderPicker.SelectedIndex switch
        {
            1 => 5,
            2 => 15,
            3 => 30,
            4 => 60,
            5 => 1440,
            _ => null
        };
    }

    private static int GetRecurrenceIndex(string? rule)
    {
        if (string.IsNullOrEmpty(rule)) return 0;
        if (rule.Contains("FREQ=DAILY")) return 1;
        if (rule.Contains("FREQ=WEEKLY") && rule.Contains("INTERVAL=2")) return 3;
        if (rule.Contains("FREQ=WEEKLY")) return 2;
        if (rule.Contains("FREQ=MONTHLY")) return 4;
        if (rule.Contains("FREQ=YEARLY")) return 5;
        return 0;
    }

    private static int GetReminderIndex(int? minutes)
    {
        return minutes switch
        {
            5 => 1,
            15 => 2,
            30 => 3,
            60 => 4,
            1440 => 5,
            _ => 0
        };
    }

    private static TimeSpan RoundToNext15(TimeSpan time)
    {
        var totalMinutes = (int)time.TotalMinutes;
        var rounded = ((totalMinutes + 14) / 15) * 15;
        return TimeSpan.FromMinutes(rounded);
    }

    private void ShowLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;
        });
    }
}
