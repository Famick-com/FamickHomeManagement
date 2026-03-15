using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Tasks;

public partial class TasksListPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private bool _includeCompleted;

    public ObservableCollection<TodoItemDisplayModel> Tasks { get; } = new();

    public TasksListPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        TasksCollection.ItemsSource = Tasks;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTasksAsync();
    }

    private async Task LoadTasksAsync()
    {
        ShowLoading();

        var result = await _apiClient.GetTodoItemsAsync(_includeCompleted);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Tasks.Clear();
            if (result.Success && result.Data != null)
            {
                var items = result.Data.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(_currentSearchTerm))
                {
                    items = items.Where(t =>
                        (t.Description?.Contains(_currentSearchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (t.Reason?.Contains(_currentSearchTerm, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                foreach (var item in items)
                    Tasks.Add(TodoItemDisplayModel.FromDto(item));

                if (Tasks.Count > 0)
                    ShowContent();
                else
                    ShowEmpty();
            }
            else
            {
                ShowEmpty();
            }
        });
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new Timer(_ =>
        {
            _currentSearchTerm = e.NewTextValue ?? string.Empty;
            _ = LoadTasksAsync();
        }, null, 400, Timeout.Infinite);
    }

    private async void OnFilterPendingClicked(object? sender, EventArgs e)
    {
        _includeCompleted = false;
        UpdateFilterChips();
        await LoadTasksAsync();
    }

    private async void OnFilterAllClicked(object? sender, EventArgs e)
    {
        _includeCompleted = true;
        UpdateFilterChips();
        await LoadTasksAsync();
    }

    private void UpdateFilterChips()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var activeColor = Color.FromArgb("#1976D2");
        var inactiveColor = isDark ? Color.FromArgb("#555555") : Color.FromArgb("#E0E0E0");
        var activeText = Colors.White;
        var inactiveText = isDark ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#424242");

        FilterPending.BackgroundColor = !_includeCompleted ? activeColor : inactiveColor;
        FilterPending.TextColor = !_includeCompleted ? activeText : inactiveText;
        FilterAll.BackgroundColor = _includeCompleted ? activeColor : inactiveColor;
        FilterAll.TextColor = _includeCompleted ? activeText : inactiveText;
    }

    private async void OnTaskTapped(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: TodoItemDisplayModel task })
        {
            await Shell.Current.GoToAsync(nameof(TaskEditPage),
                new Dictionary<string, object> { ["TaskId"] = task.Id.ToString() });
        }
    }

    private async void OnCheckChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox { BindingContext: TodoItemDisplayModel task })
        {
            if (e.Value && !task.IsCompleted)
            {
                var result = await _apiClient.CompleteTodoItemAsync(task.Id);
                if (result.Success)
                {
                    task.IsCompleted = true;
                    if (!_includeCompleted)
                        Tasks.Remove(task);
                }
            }
        }
    }

    private async void OnCompleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: TodoItemDisplayModel task })
        {
            var result = await _apiClient.CompleteTodoItemAsync(task.Id);
            if (result.Success)
            {
                if (!_includeCompleted)
                    Tasks.Remove(task);
                else
                    await LoadTasksAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to complete task", "OK");
            }
        }
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: TodoItemDisplayModel task })
        {
            var confirm = await DisplayAlert("Delete Task",
                $"Delete \"{task.Description}\"?", "Delete", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.DeleteTodoItemAsync(task.Id);
            if (result.Success)
            {
                Tasks.Remove(task);
                if (Tasks.Count == 0) ShowEmpty();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete task", "OK");
            }
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadTasksAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnAddTaskClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(TaskEditPage));
    }

    private void ShowLoading() { LoadingIndicator.IsVisible = true; RefreshContainer.IsVisible = false; EmptyState.IsVisible = false; }
    private void ShowContent() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = true; EmptyState.IsVisible = false; }
    private void ShowEmpty() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = false; EmptyState.IsVisible = true; }
}
