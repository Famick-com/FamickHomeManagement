using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.MealPlanner;

public partial class MealPlannerPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private DateTime _weekStart;
    private int _selectedDay;
    private MealPlanMobile? _plan;
    private List<MealTypeMobile> _mealTypes = new();

    public MealPlannerPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _weekStart = GetWeekStart(DateTime.Today);
        _selectedDay = (int)DateTime.Today.DayOfWeek;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private async Task LoadDataAsync()
    {
        ShowLoading();

        var typesTask = _apiClient.GetMealTypesAsync();
        var planTask = _apiClient.GetOrCreateMealPlanAsync(_weekStart);

        await Task.WhenAll(typesTask, planTask);

        if (typesTask.Result.Success && typesTask.Result.Data != null)
            _mealTypes = typesTask.Result.Data.OrderBy(t => t.SortOrder).ToList();

        if (planTask.Result.Success && planTask.Result.Data != null)
            _plan = planTask.Result.Data;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateWeekLabel();
            BuildDayTabs();
            RenderDayContent();
        });
    }

    private void UpdateWeekLabel()
    {
        var weekEnd = _weekStart.AddDays(6);
        WeekLabel.Text = $"{_weekStart:MMM d} - {weekEnd:MMM d}";
    }

    private void BuildDayTabs()
    {
        DayTabs.Children.Clear();
        var dayNames = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        // Map: Monday=0 in our array, DayOfWeek: Mon=1, Tue=2, ... Sun=0
        // We use index 0-6 for Mon-Sun, matching DayOfWeek enum values 1-6,0
        var dayOfWeekValues = new[] { 1, 2, 3, 4, 5, 6, 0 };

        for (var i = 0; i < 7; i++)
        {
            var dayDate = _weekStart.AddDays(i);
            var dayOfWeek = dayOfWeekValues[i];
            var isSelected = dayOfWeek == _selectedDay;
            var entryCount = _plan?.Entries.Count(e => e.DayOfWeek == dayOfWeek) ?? 0;

            var btn = new Button
            {
                Text = $"{dayNames[i]}\n{dayDate:d}",
                FontSize = 11,
                Padding = new Thickness(12, 8),
                CornerRadius = 8,
                Margin = new Thickness(2, 5),
                BackgroundColor = isSelected
                    ? Color.FromArgb("#1976D2")
                    : Colors.Transparent,
                TextColor = isSelected
                    ? Colors.White
                    : Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#E0E0E0")
                        : Color.FromArgb("#424242"),
                CommandParameter = dayOfWeek
            };
            btn.Clicked += OnDayTabClicked;
            DayTabs.Children.Add(btn);
        }
    }

    private void OnDayTabClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is int day)
        {
            _selectedDay = day;
            BuildDayTabs();
            RenderDayContent();
        }
    }

    private void RenderDayContent()
    {
        DayContent.Children.Clear();

        var dayEntries = _plan?.Entries
            .Where(e => e.DayOfWeek == _selectedDay)
            .OrderBy(e => _mealTypes.FindIndex(t => t.Id == e.MealTypeId))
            .ThenBy(e => e.SortOrder)
            .ToList() ?? new();

        if (!dayEntries.Any())
        {
            ShowEmpty();
            return;
        }

        ShowContent();

        // Group by meal type
        var grouped = dayEntries.GroupBy(e => e.MealTypeId);
        foreach (var group in grouped)
        {
            var mealType = _mealTypes.FirstOrDefault(t => t.Id == group.Key);
            var typeColor = mealType?.Color ?? "#4CAF50";

            // Meal type header
            var header = new Label
            {
                Text = mealType?.Name ?? "Meal",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(typeColor),
                Margin = new Thickness(0, 5, 0, 2)
            };
            DayContent.Children.Add(header);

            // Entries
            foreach (var entry in group)
            {
                var card = CreateEntryCard(entry);
                DayContent.Children.Add(card);
            }
        }

        // Add button at bottom
        var addBtn = new Button
        {
            Text = "+ Add Meal",
            BackgroundColor = Colors.Transparent,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#90CAF9") : Color.FromArgb("#1976D2"),
            FontSize = 14,
            Margin = new Thickness(0, 10, 0, 0)
        };
        addBtn.Clicked += async (s, e) => await AddEntryAsync();
        DayContent.Children.Add(addBtn);
    }

    private Border CreateEntryCard(MealPlanEntryMobile entry)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(12),
        };

        var nameStack = new VerticalStackLayout { Spacing = 2 };
        nameStack.Children.Add(new Label
        {
            Text = entry.DisplayName,
            FontSize = 15,
            FontAttributes = entry.IsInlineNote ? FontAttributes.Italic : FontAttributes.None,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Colors.White : Colors.Black
        });

        if (entry.IsBatchSource)
        {
            nameStack.Children.Add(new Label
            {
                Text = "🍳 Batch cooking",
                FontSize = 11,
                TextColor = Color.FromArgb("#FF9800")
            });
        }
        else if (entry.BatchSourceEntryId != null)
        {
            nameStack.Children.Add(new Label
            {
                Text = "📦 From batch",
                FontSize = 11,
                TextColor = Color.FromArgb("#9E9E9E")
            });
        }

        grid.Children.Add(nameStack);
        Grid.SetColumn(nameStack, 0);

        var deleteBtn = new Button
        {
            Text = "✕",
            FontSize = 14,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#D32F2F"),
            Padding = new Thickness(8, 4),
            VerticalOptions = LayoutOptions.Center
        };
        deleteBtn.Clicked += async (s, e) => await DeleteEntryAsync(entry);
        grid.Children.Add(deleteBtn);
        Grid.SetColumn(deleteBtn, 1);

        return new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Stroke = Colors.Transparent,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2A2A2A") : Colors.White,
            Margin = new Thickness(0, 2),
            Content = grid,
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Offset = new Point(0, 1),
                Radius = 4,
                Opacity = 0.1f
            }
        };
    }

    private async Task AddEntryAsync()
    {
        if (_plan == null || !_mealTypes.Any()) return;

        // Show action sheet to pick meal type
        var mealTypeNames = _mealTypes.Select(t => t.Name).ToArray();
        var selectedType = await DisplayActionSheet("Select Meal Type", "Cancel", null, mealTypeNames);
        if (selectedType == null || selectedType == "Cancel") return;

        var mealType = _mealTypes.FirstOrDefault(t => t.Name == selectedType);
        if (mealType == null) return;

        // Show action sheet: Add meal or add note
        var action = await DisplayActionSheet("Add Entry", "Cancel", null, "Select a Meal", "Add a Note");
        if (action == null || action == "Cancel") return;

        if (action == "Add a Note")
        {
            var note = await DisplayPromptAsync("Add Note", "Enter a note for this meal slot:", maxLength: 200);
            if (string.IsNullOrWhiteSpace(note)) return;

            var request = new CreateMealPlanEntryRequest
            {
                InlineNote = note,
                MealTypeId = mealType.Id,
                DayOfWeek = _selectedDay
            };
            var result = await _apiClient.AddMealPlanEntryAsync(_plan.Id, request);
            if (result.Success && result.Data != null)
            {
                _plan.Entries.Add(result.Data);
                RenderDayContent();
            }
        }
        else
        {
            // Navigate to meal selection
            await Shell.Current.GoToAsync(nameof(MealSelectionPage), new Dictionary<string, object>
            {
                ["PlanId"] = _plan.Id,
                ["MealTypeId"] = mealType.Id,
                ["DayOfWeek"] = _selectedDay,
                ["OnEntryAdded"] = new Action<MealPlanEntryMobile>(entry =>
                {
                    _plan.Entries.Add(entry);
                    MainThread.BeginInvokeOnMainThread(RenderDayContent);
                })
            });
        }
    }

    private async Task DeleteEntryAsync(MealPlanEntryMobile entry)
    {
        if (_plan == null) return;

        var confirmed = await DisplayAlert("Delete Entry", $"Remove \"{entry.DisplayName}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        var result = await _apiClient.DeleteMealPlanEntryAsync(_plan.Id, entry.Id);
        if (result.Success)
        {
            _plan.Entries.Remove(entry);
            RenderDayContent();
        }
    }

    private async void OnPreviousWeekClicked(object? sender, EventArgs e)
    {
        _weekStart = _weekStart.AddDays(-7);
        await LoadDataAsync();
    }

    private async void OnNextWeekClicked(object? sender, EventArgs e)
    {
        _weekStart = _weekStart.AddDays(7);
        await LoadDataAsync();
    }

    private async void OnAddEntryClicked(object? sender, EventArgs e)
    {
        await AddEntryAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadDataAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        RefreshContainer.IsVisible = false;
        EmptyState.IsVisible = false;
    }

    private void ShowContent()
    {
        LoadingIndicator.IsVisible = false;
        RefreshContainer.IsVisible = true;
        EmptyState.IsVisible = false;
    }

    private void ShowEmpty()
    {
        LoadingIndicator.IsVisible = false;
        RefreshContainer.IsVisible = false;
        EmptyState.IsVisible = true;
    }
}
