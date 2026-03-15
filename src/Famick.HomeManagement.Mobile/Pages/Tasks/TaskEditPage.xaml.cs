using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Tasks;

[QueryProperty(nameof(TaskId), "TaskId")]
public partial class TaskEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private TodoItemDto? _existingItem;
    private bool _isEditMode;

    public string TaskId { get; set; } = string.Empty;

    private static readonly Dictionary<string, string> TaskTypeMap = new()
    {
        ["Other"] = "Other",
        ["Inventory"] = "Inventory",
        ["Product"] = "Product",
        ["Equipment"] = "Equipment"
    };

    private static readonly Dictionary<string, int> TaskTypeToPickerIndex = new()
    {
        ["Other"] = 0,
        ["Inventory"] = 1,
        ["Product"] = 2,
        ["Equipment"] = 3
    };

    public TaskEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (Guid.TryParse(TaskId, out var taskId))
        {
            _isEditMode = true;
            Title = "Edit Task";
            await LoadTaskAsync(taskId);
        }
        else
        {
            _isEditMode = false;
            Title = "New Task";
            TaskTypePicker.SelectedIndex = 0; // Default to "Other"
            LoadingIndicator.IsVisible = false;
            ContentScroll.IsVisible = true;
        }
    }

    private async Task LoadTaskAsync(Guid taskId)
    {
        var result = await _apiClient.GetTodoItemAsync(taskId);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = false;
            ContentScroll.IsVisible = true;

            if (result.Success && result.Data != null)
            {
                _existingItem = result.Data;
                DescriptionEditor.Text = _existingItem.Description ?? string.Empty;
                ReasonEditor.Text = _existingItem.Reason ?? string.Empty;

                if (_existingItem.TaskTypeName != null &&
                    TaskTypeToPickerIndex.TryGetValue(_existingItem.TaskTypeName, out var idx))
                    TaskTypePicker.SelectedIndex = idx;
                else
                    TaskTypePicker.SelectedIndex = 0;

                if (_existingItem.IsCompleted)
                {
                    CompletedSection.IsVisible = true;
                    CompletedLabel.Text = $"Completed {_existingItem.CompletedAt?.ToString("MMM d, yyyy") ?? ""}";
                }
            }
        });
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var description = DescriptionEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            await DisplayAlert("Required", "Please enter a description.", "OK");
            return;
        }

        var taskType = TaskTypePicker.SelectedItem?.ToString() ?? "Other";
        var reason = ReasonEditor.Text?.Trim() ?? string.Empty;

        SaveButton.IsEnabled = false;

        try
        {
            if (_isEditMode && _existingItem != null)
            {
                var request = new UpdateTodoItemMobileRequest
                {
                    TaskType = taskType,
                    Description = description,
                    Reason = reason
                };

                var result = await _apiClient.UpdateTodoItemAsync(_existingItem.Id, request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update task.", "OK");
                }
            }
            else
            {
                var request = new CreateTodoItemRequest
                {
                    TaskType = taskType,
                    Description = description,
                    Reason = reason
                };

                var result = await _apiClient.CreateTodoItemAsync(request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create task.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error: {ex.Message}", "OK");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }
}
