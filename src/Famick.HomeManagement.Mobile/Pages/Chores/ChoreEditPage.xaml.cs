using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Chores;

[QueryProperty(nameof(ChoreId), "ChoreId")]
public partial class ChoreEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ChoreDetailItem? _chore;
    private bool _isEditMode;
    private bool _loaded;
    private List<HouseholdMember> _allMembers = new();
    private readonly HashSet<Guid> _selectedMemberIds = new();

    private static readonly string[] PeriodTypeValues =
        { "manually", "daily", "weekly", "monthly", "dynamic-regular" };

    public string ChoreId { get; set; } = string.Empty;

    public ChoreEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        StartDatePicker.Date = DateTime.Now.Date;
        StartTimePicker.Time = new TimeSpan(9, 0, 0);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded) return;
        _loaded = true;

        _isEditMode = !string.IsNullOrEmpty(ChoreId) && Guid.TryParse(ChoreId, out _);

        // Load household members for assignment
        var membersResult = await _apiClient.GetCalendarMembersAsync();
        if (membersResult.Success && membersResult.Data != null)
        {
            _allMembers = membersResult.Data;
        }

        if (_isEditMode)
        {
            TitleLabel.Text = "Edit Chore";
            await LoadChoreAsync();
        }
        else
        {
            TitleLabel.Text = "New Chore";
            PeriodTypePicker.SelectedIndex = 0; // Manual

            // Default assignment to current user
            var currentUser = _allMembers.FirstOrDefault(m => m.IsCurrentUser);
            if (currentUser != null)
            {
                _selectedMemberIds.Add(currentUser.Id);
            }

            RenderMembers();
        }
    }

    private async Task LoadChoreAsync()
    {
        if (!Guid.TryParse(ChoreId, out var id)) return;

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;

        try
        {
            var result = await _apiClient.GetChoreAsync(id);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _chore = result.Data;
                    PopulateForm();
                }

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ContentScroll.IsVisible = true;
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ContentScroll.IsVisible = true;
                _ = DisplayAlert("Error", $"Failed to load chore: {ex.Message}", "OK");
            });
        }
    }

    private void PopulateForm()
    {
        if (_chore == null) return;

        NameEntry.Text = _chore.Name;
        DescriptionEditor.Text = _chore.Description;
        RolloverSwitch.IsToggled = _chore.Rollover;
        TrackDateOnlySwitch.IsToggled = _chore.TrackDateOnly;

        // Set period type picker
        var periodIndex = _chore.PeriodType.ToLowerInvariant() switch
        {
            "manually" => 0,
            "daily" => 1,
            "weekly" => 2,
            "monthly" => 3,
            "dynamic-regular" => 4,
            _ => 0
        };
        PeriodTypePicker.SelectedIndex = periodIndex;

        if (_chore.PeriodDays.HasValue)
        {
            PeriodDaysEntry.Text = _chore.PeriodDays.Value.ToString();
        }

        // Start date
        if (_chore.StartDate.HasValue)
        {
            var local = _chore.StartDate.Value.ToLocalTime();
            StartDatePicker.Date = local.Date;

            if (local.TimeOfDay != TimeSpan.Zero)
            {
                IncludeTimeSwitch.IsToggled = true;
                TimeSection.IsVisible = true;
                StartTimePicker.Time = local.TimeOfDay;
            }
        }

        // Assignment - parse existing config
        _selectedMemberIds.Clear();
        if (!string.IsNullOrEmpty(_chore.AssignmentConfig))
        {
            var ids = ParseAssignmentConfig(_chore.AssignmentConfig);
            foreach (var id in ids)
                _selectedMemberIds.Add(id);
        }
        else if (_chore.NextExecutionAssignedToUserId.HasValue)
        {
            _selectedMemberIds.Add(_chore.NextExecutionAssignedToUserId.Value);
        }

        RenderMembers();
    }

    private void RenderMembers()
    {
        MembersList.Children.Clear();

        foreach (var member in _allMembers)
        {
            var isSelected = _selectedMemberIds.Contains(member.Id);
            var memberId = member.Id;

            var card = new Border
            {
                Padding = new Thickness(12, 8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5")
            };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            var nameStack = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
            nameStack.Children.Add(new Label
            {
                Text = member.DisplayName,
                FontSize = 15,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.White : Colors.Black,
                VerticalOptions = LayoutOptions.Center
            });

            if (member.IsCurrentUser)
            {
                nameStack.Children.Add(new Label
                {
                    Text = "(you)",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#888888"),
                    VerticalOptions = LayoutOptions.Center
                });
            }

            row.Children.Add(nameStack);
            Grid.SetColumn(nameStack, 0);

            var checkbox = new CheckBox
            {
                IsChecked = isSelected,
                Color = Color.FromArgb("#1976D2"),
                VerticalOptions = LayoutOptions.Center
            };
            checkbox.CheckedChanged += (_, args) =>
            {
                if (args.Value)
                    _selectedMemberIds.Add(memberId);
                else
                    _selectedMemberIds.Remove(memberId);
            };
            row.Children.Add(checkbox);
            Grid.SetColumn(checkbox, 1);

            card.Content = row;
            MembersList.Children.Add(card);
        }
    }

    private void OnPeriodTypeChanged(object? sender, EventArgs e)
    {
        var selectedIndex = PeriodTypePicker.SelectedIndex;
        // Show period days for "Custom interval" (4) and "Monthly" (3)
        var showDays = selectedIndex == 3 || selectedIndex == 4;
        PeriodDaysSection.IsVisible = showDays;
        PeriodDaysLabel.Text = selectedIndex == 3 ? "Day of month" : "Every N days";
    }

    private void OnIncludeTimeToggled(object? sender, ToggledEventArgs e)
    {
        TimeSection.IsVisible = e.Value;
    }

    private DateTime? BuildStartDate()
    {
        var date = StartDatePicker.Date ?? DateTime.Now.Date;

        if (IncludeTimeSwitch.IsToggled)
        {
            var time = StartTimePicker.Time ?? new TimeSpan(9, 0, 0);
            var local = new DateTime(date.Year, date.Month, date.Day,
                time.Hours, time.Minutes, 0, DateTimeKind.Local);
            return local.ToUniversalTime();
        }

        // Date only - store as midnight local time converted to UTC
        // so that ToLocalTime() round-trips back to the correct calendar date
        var localMidnight = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local);
        return localMidnight.ToUniversalTime();
    }

    private string? BuildAssignmentType()
    {
        if (_selectedMemberIds.Count == 0) return null;
        if (_selectedMemberIds.Count == 1) return "specific-user";
        return "round-robin";
    }

    private string? BuildAssignmentConfig()
    {
        if (_selectedMemberIds.Count == 0) return null;
        if (_selectedMemberIds.Count == 1) return _selectedMemberIds.First().ToString();
        return System.Text.Json.JsonSerializer.Serialize(_selectedMemberIds.ToList());
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Validation", "Chore name is required.", "OK");
            return;
        }

        var periodTypeIndex = PeriodTypePicker.SelectedIndex;
        if (periodTypeIndex < 0) periodTypeIndex = 0;
        var periodType = PeriodTypeValues[periodTypeIndex];

        int? periodDays = null;
        if (PeriodDaysSection.IsVisible && !string.IsNullOrEmpty(PeriodDaysEntry.Text))
        {
            if (int.TryParse(PeriodDaysEntry.Text, out var days) && days > 0)
            {
                periodDays = days;
            }
            else
            {
                await DisplayAlert("Validation", "Period days must be a positive number.", "OK");
                return;
            }
        }

        if ((periodType == "dynamic-regular" || periodType == "monthly") && !periodDays.HasValue)
        {
            await DisplayAlert("Validation", "Period days is required for this frequency.", "OK");
            return;
        }

        SaveToolbarItem.IsEnabled = false;

        try
        {
            if (_isEditMode && _chore != null)
            {
                var request = new UpdateChoreMobileRequest
                {
                    Name = name,
                    Description = DescriptionEditor.Text?.Trim(),
                    PeriodType = periodType,
                    PeriodDays = periodDays,
                    StartDate = BuildStartDate(),
                    Rollover = RolloverSwitch.IsToggled,
                    TrackDateOnly = TrackDateOnlySwitch.IsToggled,
                    AssignmentType = BuildAssignmentType(),
                    AssignmentConfig = BuildAssignmentConfig()
                };

                var result = await _apiClient.UpdateChoreAsync(_chore.Id, request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update chore", "OK");
            }
            else
            {
                var request = new CreateChoreMobileRequest
                {
                    Name = name,
                    Description = DescriptionEditor.Text?.Trim(),
                    PeriodType = periodType,
                    PeriodDays = periodDays,
                    StartDate = BuildStartDate(),
                    Rollover = RolloverSwitch.IsToggled,
                    TrackDateOnly = TrackDateOnlySwitch.IsToggled,
                    AssignmentType = BuildAssignmentType(),
                    AssignmentConfig = BuildAssignmentConfig()
                };

                var result = await _apiClient.CreateChoreAsync(request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create chore", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

        SaveToolbarItem.IsEnabled = true;
    }

    private static List<Guid> ParseAssignmentConfig(string config)
    {
        if (config.StartsWith("["))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(config) ?? new();
            }
            catch { }
        }

        return config.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s.Trim().Trim('"'), out var guid) ? guid : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();
    }
}
