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
    private int _selectedPlanningStyle = 1; // Default: WeekAtAGlance
    private int _onboardingStep;
    private bool _onboardingChecked;
    private List<(string Name, string Color, bool Selected)> _onboardingMealTypes = new()
    {
        ("Breakfast", "#FFA726", true),
        ("Lunch", "#66BB6A", true),
        ("Dinner", "#42A5F5", true),
        ("Supper", "#5C6BC0", false),
        ("Snack", "#AB47BC", true)
    };

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

        if (!_onboardingChecked)
        {
            await CheckOnboardingAsync();
            _onboardingChecked = true;
        }
        else
        {
            await LoadDataAsync();
        }
    }

    #region Onboarding

    private async Task CheckOnboardingAsync()
    {
        InitialLoading.IsVisible = true;
        OnboardingView.IsVisible = false;
        PlannerView.IsVisible = false;

        try
        {
            var result = await _apiClient.GetMealPlannerOnboardingAsync();
            if (result.Success && result.Data != null && !result.Data.HasCompletedOnboarding)
            {
                InitialLoading.IsVisible = false;
                _onboardingStep = 0;
                ShowOnboardingStep();
                OnboardingView.IsVisible = true;
                return;
            }
        }
        catch
        {
            // Proceed to planner on error
        }

        InitialLoading.IsVisible = false;
        PlannerView.IsVisible = true;
        await LoadDataAsync();
    }

    private void ShowOnboardingStep()
    {
        OnboardingContent.Children.Clear();
        UpdateStepDots();

        switch (_onboardingStep)
        {
            case 0:
                BuildWelcomeStep();
                OnboardingBackButton.Text = "Skip";
                OnboardingNextButton.Text = "Next";
                break;
            case 1:
                BuildMealTypesStep();
                OnboardingBackButton.Text = "Back";
                OnboardingNextButton.Text = "Next";
                break;
            case 2:
                BuildCompleteStep();
                OnboardingBackButton.Text = "Back";
                OnboardingNextButton.Text = "Get Started";
                break;
        }
    }

    private void UpdateStepDots()
    {
        var active = Color.FromArgb("#1976D2");
        var inactive = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");

        Step1Dot.Color = _onboardingStep >= 0 ? active : inactive;
        Step2Dot.Color = _onboardingStep >= 1 ? active : inactive;
        Step3Dot.Color = _onboardingStep >= 2 ? active : inactive;
    }

    private void BuildWelcomeStep()
    {
        var content = OnboardingContent;

        content.Children.Add(new Label
        {
            Text = "🍽️", FontSize = 64, HorizontalOptions = LayoutOptions.Center
        });
        content.Children.Add(new Label
        {
            Text = "Welcome to Meal Planner",
            FontSize = 24, FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        });
        content.Children.Add(new Label
        {
            Text = "Plan your meals for the week, track nutrition, and generate shopping lists.",
            FontSize = 14, HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#999999" : "#666666")
        });

        content.Children.Add(new BoxView
        {
            HeightRequest = 1, Margin = new Thickness(0, 10),
            BackgroundColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#424242" : "#E0E0E0")
        });

        content.Children.Add(new Label
        {
            Text = "How do you prefer to plan?",
            FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        });

        content.Children.Add(CreatePlanningStyleCard(
            "📅", "Day by Day", "Focus on one day at a time", 0));
        content.Children.Add(CreatePlanningStyleCard(
            "🗓️", "Week at a Glance", "See the whole week overview", 1));
    }

    private Border CreatePlanningStyleCard(string icon, string title, string description, int style)
    {
        var isSelected = _selectedPlanningStyle == style;
        var selectedStroke = Color.FromArgb("#1976D2");
        var unselectedStroke = Color.FromArgb(
            Application.Current?.RequestedTheme == AppTheme.Dark ? "#424242" : "#E0E0E0");

        var card = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(16),
            Margin = new Thickness(0, 4),
            Stroke = isSelected ? selectedStroke : unselectedStroke,
            StrokeThickness = isSelected ? 2 : 1,
            BackgroundColor = Color.FromArgb(
                Application.Current?.RequestedTheme == AppTheme.Dark ? "#1E1E1E" : "#FFFFFF")
        };

        var stack = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center };
        stack.Children.Add(new Label { Text = icon, FontSize = 32, HorizontalOptions = LayoutOptions.Center });
        stack.Children.Add(new Label
        {
            Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        });
        stack.Children.Add(new Label
        {
            Text = description, FontSize = 13, HorizontalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#999999" : "#666666")
        });

        card.Content = stack;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            _selectedPlanningStyle = style;
            ShowOnboardingStep(); // Rebuild to update selection
        };
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private void BuildMealTypesStep()
    {
        var content = OnboardingContent;

        content.Children.Add(new Label
        {
            Text = "Which meals do you plan?",
            FontSize = 24, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        });
        content.Children.Add(new Label
        {
            Text = "Select the meal types you want to plan for. You can change these later in Settings.",
            FontSize = 14, HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#999999" : "#666666")
        });

        content.Children.Add(new BoxView
        {
            HeightRequest = 1, Margin = new Thickness(0, 10),
            BackgroundColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#424242" : "#E0E0E0")
        });

        for (var i = 0; i < _onboardingMealTypes.Count; i++)
        {
            var index = i;
            var (name, color, selected) = _onboardingMealTypes[index];

            var card = new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Stroke = selected ? Color.FromArgb("#1976D2") : Colors.Transparent,
                StrokeThickness = selected ? 2 : 0,
                Padding = new Thickness(16, 12),
                Margin = new Thickness(0, 4),
                BackgroundColor = Color.FromArgb(
                    Application.Current?.RequestedTheme == AppTheme.Dark ? "#1E1E1E" : "#FFFFFF")
            };

            var row = new HorizontalStackLayout { Spacing = 12, VerticalOptions = LayoutOptions.Center };

            var checkBox = new CheckBox
            {
                IsChecked = selected,
                Color = Color.FromArgb("#1976D2"),
                VerticalOptions = LayoutOptions.Center
            };
            checkBox.CheckedChanged += (_, args) =>
            {
                _onboardingMealTypes[index] = (name, color, args.Value);
                UpdateMealTypeNextButton();
                ShowOnboardingStep(); // Rebuild to update stroke
            };
            row.Children.Add(checkBox);

            row.Children.Add(new BoxView
            {
                WidthRequest = 4, HeightRequest = 24, CornerRadius = 2,
                BackgroundColor = Color.FromArgb(color), VerticalOptions = LayoutOptions.Center
            });
            row.Children.Add(new Label
            {
                Text = name, FontSize = 16, VerticalOptions = LayoutOptions.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
            });

            card.Content = row;

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                var current = _onboardingMealTypes[index];
                _onboardingMealTypes[index] = (current.Name, current.Color, !current.Selected);
                UpdateMealTypeNextButton();
                ShowOnboardingStep();
            };
            card.GestureRecognizers.Add(tap);

            content.Children.Add(card);
        }

        UpdateMealTypeNextButton();
    }

    private void UpdateMealTypeNextButton()
    {
        if (_onboardingStep == 1)
            OnboardingNextButton.IsEnabled = _onboardingMealTypes.Any(t => t.Selected);
    }

    private void BuildCompleteStep()
    {
        var content = OnboardingContent;

        content.Children.Add(new Label
        {
            Text = "✅", FontSize = 64, HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 20, 0, 0)
        });
        content.Children.Add(new Label
        {
            Text = "You're all set!",
            FontSize = 24, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        });
        content.Children.Add(new Label
        {
            Text = "Start planning your meals for the week. You can add meals, notes, and generate shopping lists from your plan.",
            FontSize = 14, HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#999999" : "#666666"),
            Margin = new Thickness(20, 0)
        });
    }

    private void OnOnboardingBackClicked(object? sender, EventArgs e)
    {
        if (_onboardingStep == 0)
        {
            // Skip — complete immediately
            _ = CompleteOnboardingAsync();
        }
        else
        {
            _onboardingStep--;
            ShowOnboardingStep();
        }
    }

    private void OnOnboardingNextClicked(object? sender, EventArgs e)
    {
        if (_onboardingStep < 2)
        {
            _onboardingStep++;
            ShowOnboardingStep();
        }
        else
        {
            // Final step — complete
            _ = CompleteOnboardingAsync();
        }
    }

    private async Task CompleteOnboardingAsync()
    {
        OnboardingNextButton.IsEnabled = false;

        try
        {
            var request = new SaveOnboardingMobileRequest
            {
                PlanningStyle = _selectedPlanningStyle,
                MealTypes = _onboardingMealTypes
                    .Where(t => t.Selected)
                    .Select(t => new MealTypeSelection { Name = t.Name, Color = t.Color })
                    .ToList()
            };
            await _apiClient.SaveMealPlannerOnboardingAsync(request);
        }
        catch
        {
            // Continue even if save fails
        }

        OnboardingView.IsVisible = false;
        PlannerView.IsVisible = true;
        OnboardingNextButton.IsEnabled = true;
        await LoadDataAsync();
    }

    #endregion

    #region Planner

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private async Task LoadDataAsync()
    {
        ShowLoading();

        var typesResult = await _apiClient.GetMealTypesAsync();
        var planResult = await _apiClient.GetOrCreateMealPlanAsync(_weekStart);

        if (typesResult.Success && typesResult.Data != null)
            _mealTypes = typesResult.Data.OrderBy(t => t.SortOrder).ToList();

        if (planResult.Success && planResult.Data != null)
            _plan = planResult.Data;

        if (!typesResult.Success || !planResult.Success)
        {
            var errors = new List<string>();
            if (!typesResult.Success) errors.Add($"Meal types: {typesResult.ErrorMessage}");
            if (!planResult.Success) errors.Add($"Meal plan: {planResult.ErrorMessage}");
            System.Diagnostics.Debug.WriteLine($"[MealPlanner] LoadData errors: {string.Join("; ", errors)}");
        }

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
        var dayOfWeekValues = new[] { 1, 2, 3, 4, 5, 6, 0 };

        for (var i = 0; i < 7; i++)
        {
            var dayDate = _weekStart.AddDays(i);
            var dayOfWeek = dayOfWeekValues[i];
            var isSelected = dayOfWeek == _selectedDay;

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

        var grouped = dayEntries.GroupBy(e => e.MealTypeId);
        foreach (var group in grouped)
        {
            var mealType = _mealTypes.FirstOrDefault(t => t.Id == group.Key);
            var typeColor = mealType?.Color ?? "#4CAF50";

            var header = new Label
            {
                Text = mealType?.Name ?? "Meal",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(typeColor),
                Margin = new Thickness(0, 5, 0, 2)
            };
            DayContent.Children.Add(header);

            foreach (var entry in group)
            {
                var card = CreateEntryCard(entry);
                DayContent.Children.Add(card);
            }
        }

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
        if (_plan == null || !_mealTypes.Any())
        {
            var msg = _plan == null ? "No meal plan loaded." : "No meal types available.";
            await DisplayAlert("Cannot Add Meal", $"{msg} Please pull to refresh.", "OK");
            return;
        }

        MealTypeMobile mealType;

        if (_mealTypes.Count == 1)
        {
            mealType = _mealTypes[0];
        }
        else
        {
            var mealTypeNames = _mealTypes.Select(t => t.Name).ToArray();
            var selectedType = await DisplayActionSheet("Select Meal Type", "Cancel", null, mealTypeNames);
            if (selectedType == null || selectedType == "Cancel") return;

            mealType = _mealTypes.FirstOrDefault(t => t.Name == selectedType)!;
            if (mealType == null) return;
        }

        await Shell.Current.GoToAsync(nameof(MealSelectionPage), new Dictionary<string, object>
        {
            ["PlanId"] = _plan.Id,
            ["MealTypeId"] = mealType.Id,
            ["DayOfWeek"] = _selectedDay,
            ["Version"] = _plan.Version
        });
    }

    private async Task DeleteEntryAsync(MealPlanEntryMobile entry)
    {
        if (_plan == null) return;

        var confirmed = await DisplayAlert("Delete Entry", $"Remove \"{entry.DisplayName}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        var result = await _apiClient.DeleteMealPlanEntryAsync(_plan.Id, entry.Id, _plan.Version);
        if (result.Success)
        {
            await LoadDataAsync(); // Reload to get updated version
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete entry", "OK");
            await LoadDataAsync();
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

    #endregion
}
