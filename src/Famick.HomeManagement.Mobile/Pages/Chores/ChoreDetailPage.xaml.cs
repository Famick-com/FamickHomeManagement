using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Chores;

[QueryProperty(nameof(ChoreId), "ChoreId")]
public partial class ChoreDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ChoreDetailItem? _chore;

    public string ChoreId { get; set; } = string.Empty;

    public ChoreDetailPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadChoreAsync();
    }

    private async Task LoadChoreAsync()
    {
        if (!Guid.TryParse(ChoreId, out var id))
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError("Invalid chore ID"));
            return;
        }

        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var choreResult = await _apiClient.GetChoreAsync(id);
            if (choreResult.Success && choreResult.Data != null)
            {
                _chore = choreResult.Data;

                var logsResult = await _apiClient.GetChoreLogsAsync(id, 20);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RenderChore();
                    RenderHistory(logsResult.Success ? logsResult.Data : null);
                    ShowContent();
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError(choreResult.ErrorMessage ?? "Failed to load chore"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                ShowError($"Connection error: {ex.Message}"));
        }
    }

    private void RenderChore()
    {
        if (_chore == null) return;

        // Reset visibility for re-render
        DescriptionSection.IsVisible = false;
        StartDateRow.IsVisible = false;
        OverdueBadge.IsVisible = false;
        AssignedSection.IsVisible = false;
        ProductSection.IsVisible = false;

        TitleLabel.Text = _chore.Name;

        // Description
        if (!string.IsNullOrWhiteSpace(_chore.Description))
        {
            DescriptionLabel.Text = _chore.Description;
            DescriptionSection.IsVisible = true;
        }

        // Schedule
        ScheduleLabel.Text = FormatSchedule(_chore.PeriodType, _chore.PeriodDays);

        // Start date
        if (_chore.StartDate.HasValue)
        {
            var local = _chore.StartDate.Value.ToLocalTime();
            StartDateLabel.Text = local.TimeOfDay != TimeSpan.Zero
                ? local.ToString("dddd, MMMM d, yyyy h:mm tt")
                : local.ToString("dddd, MMMM d, yyyy");
            StartDateRow.IsVisible = true;
        }

        // Next due
        if (_chore.NextExecutionDate.HasValue)
        {
            NextDueLabel.Text = _chore.NextExecutionDate.Value.ToLocalTime().ToString("dddd, MMMM d, yyyy");
            OverdueBadge.IsVisible = _chore.IsOverdue;
        }
        else
        {
            NextDueLabel.Text = "Not scheduled";
        }

        // Assigned user
        if (!string.IsNullOrEmpty(_chore.NextExecutionAssignedToUserName))
        {
            AssignedLabel.Text = _chore.NextExecutionAssignedToUserName;
            AssignedSection.IsVisible = true;
        }

        // Product
        if (_chore.ConsumeProductOnExecution && !string.IsNullOrEmpty(_chore.ProductName))
        {
            var productText = _chore.ProductName;
            if (_chore.ProductAmount.HasValue)
                productText += $" (x{_chore.ProductAmount.Value})";
            ProductLabel.Text = productText;
            ProductSection.IsVisible = true;
        }
    }

    private void RenderHistory(List<ChoreLogItem>? logs)
    {
        HistoryList.Children.Clear();

        if (logs == null || logs.Count == 0)
        {
            NoHistoryLabel.IsVisible = true;
            return;
        }

        NoHistoryLabel.IsVisible = false;

        foreach (var log in logs)
        {
            var card = new Border
            {
                Padding = new Thickness(12, 8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F5F5F5")
            };

            var swipe = new SwipeView();
            if (!log.Undone && !log.Skipped)
            {
                var undoItem = new SwipeItem
                {
                    Text = "Undo",
                    BackgroundColor = Color.FromArgb("#FF9800"),
                    BindingContext = log
                };
                undoItem.Invoked += OnUndoLogSwiped;
                swipe.RightItems = new SwipeItems { undoItem };
            }

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            var leftStack = new VerticalStackLayout { Spacing = 2 };
            leftStack.Children.Add(new Label
            {
                Text = log.DateDisplay,
                FontSize = 14,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.White : Colors.Black
            });

            if (!string.IsNullOrEmpty(log.DoneByUserName))
            {
                leftStack.Children.Add(new Label
                {
                    Text = log.DoneByUserName,
                    FontSize = 12,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#999999") : Color.FromArgb("#888888")
                });
            }

            row.Children.Add(leftStack);
            Grid.SetColumn(leftStack, 0);

            var statusBadge = new Border
            {
                Padding = new Thickness(6, 2),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                VerticalOptions = LayoutOptions.Center,
                BackgroundColor = log.Undone
                    ? Color.FromArgb("#FFF3E0")
                    : log.Skipped
                        ? Color.FromArgb("#EEEEEE")
                        : Color.FromArgb("#E8F5E9"),
                Content = new Label
                {
                    Text = log.StatusDisplay,
                    FontSize = 11,
                    TextColor = log.Undone
                        ? Color.FromArgb("#E65100")
                        : log.Skipped
                            ? Color.FromArgb("#757575")
                            : Color.FromArgb("#2E7D32")
                }
            };
            row.Children.Add(statusBadge);
            Grid.SetColumn(statusBadge, 1);

            card.Content = row;
            swipe.Content = card;
            HistoryList.Children.Add(swipe);
        }
    }

    private static string FormatSchedule(string periodType, int? periodDays)
    {
        return periodType.ToLowerInvariant() switch
        {
            "daily" => "Daily",
            "weekly" => "Weekly",
            "monthly" => "Monthly",
            "yearly" => "Yearly",
            "manually" => "Manual",
            _ when periodDays.HasValue => $"Every {periodDays.Value} days",
            _ => periodType
        };
    }

    private async void OnMarkDoneClicked(object? sender, EventArgs e)
    {
        if (_chore == null) return;

        MarkDoneButton.IsEnabled = false;
        var result = await _apiClient.ExecuteChoreAsync(_chore.Id);
        MarkDoneButton.IsEnabled = true;

        if (result.Success)
        {
            await LoadChoreAsync();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to mark chore as done", "OK");
        }
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        if (_chore == null) return;

        SkipButton.IsEnabled = false;
        var result = await _apiClient.SkipChoreAsync(_chore.Id);
        SkipButton.IsEnabled = true;

        if (result.Success)
        {
            await LoadChoreAsync();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to skip chore", "OK");
        }
    }

    private async void OnUndoLogSwiped(object? sender, EventArgs e)
    {
        if (_chore == null) return;
        if (sender is SwipeItem { BindingContext: ChoreLogItem log })
        {
            var confirmed = await DisplayAlert("Undo", "Undo this execution?", "Undo", "Cancel");
            if (!confirmed) return;

            var result = await _apiClient.UndoChoreExecutionAsync(_chore.Id, log.Id);
            if (result.Success)
            {
                await LoadChoreAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to undo execution", "OK");
            }
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_chore == null) return;
        await Shell.Current.GoToAsync(nameof(ChoreEditPage),
            new Dictionary<string, object> { ["ChoreId"] = _chore.Id.ToString() });
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_chore == null) return;

        var confirmed = await DisplayAlert("Delete Chore",
            $"Are you sure you want to delete \"{_chore.Name}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        var result = await _apiClient.DeleteChoreAsync(_chore.Id);
        if (result.Success)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete chore", "OK");
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadChoreAsync();
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
