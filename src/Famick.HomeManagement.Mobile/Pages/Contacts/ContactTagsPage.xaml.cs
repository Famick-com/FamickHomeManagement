using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

public partial class ContactTagsPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Guid? _editingTagId;

    public ObservableCollection<ContactTagDto> Tags { get; } = new();

    public ContactTagsPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        TagsCollection.ItemsSource = Tags;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTagsAsync();
    }

    private async Task LoadTagsAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            RefreshContainer.IsVisible = false;
            EmptyLabel.IsVisible = false;
        });

        try
        {
            var result = await _apiClient.GetContactTagsAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;

                if (result.Success && result.Data != null)
                {
                    Tags.Clear();
                    foreach (var tag in result.Data)
                        Tags.Add(tag);

                    if (Tags.Count > 0)
                        RefreshContainer.IsVisible = true;
                    else
                        EmptyLabel.IsVisible = true;
                }
                else
                {
                    EmptyLabel.IsVisible = true;
                }
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                EmptyLabel.IsVisible = true;
            });
        }
    }

    private void OnAddTagClicked(object? sender, EventArgs e)
    {
        _editingTagId = null;
        EditorTitle.Text = "New Tag";
        TagNameEntry.Text = string.Empty;
        TagDescriptionEntry.Text = string.Empty;
        TagColorEntry.Text = "#9E9E9E";
        EditorPanel.IsVisible = true;
    }

    private void OnEditTagSwiped(object? sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: ContactTagDto tag }) return;
        _editingTagId = tag.Id;
        EditorTitle.Text = "Edit Tag";
        TagNameEntry.Text = tag.Name;
        TagDescriptionEntry.Text = tag.Description;
        TagColorEntry.Text = tag.Color ?? "#9E9E9E";
        EditorPanel.IsVisible = true;
    }

    private async void OnDeleteTagSwiped(object? sender, EventArgs e)
    {
        if (sender is not SwipeItem { BindingContext: ContactTagDto tag }) return;

        var confirm = await DisplayAlert("Delete Tag",
            $"Delete \"{tag.Name}\"? This will remove it from all contacts.", "Delete", "Cancel");
        if (!confirm) return;

        var result = await _apiClient.DeleteContactTagAsync(tag.Id);
        if (result.Success)
        {
            Tags.Remove(tag);
            if (Tags.Count == 0)
            {
                RefreshContainer.IsVisible = false;
                EmptyLabel.IsVisible = true;
            }
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete tag", "OK");
        }
    }

    private void OnEditorCancelClicked(object? sender, EventArgs e)
    {
        EditorPanel.IsVisible = false;
    }

    private async void OnEditorSaveClicked(object? sender, EventArgs e)
    {
        var name = TagNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Validation", "Tag name is required.", "OK");
            return;
        }

        if (_editingTagId.HasValue)
        {
            var result = await _apiClient.UpdateContactTagAsync(_editingTagId.Value, new UpdateContactTagRequest
            {
                Name = name,
                Description = TagDescriptionEntry.Text?.Trim(),
                Color = TagColorEntry.Text?.Trim()
            });
            if (result.Success)
            {
                EditorPanel.IsVisible = false;
                await LoadTagsAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update tag", "OK");
            }
        }
        else
        {
            var result = await _apiClient.CreateContactTagAsync(new CreateContactTagRequest
            {
                Name = name,
                Description = TagDescriptionEntry.Text?.Trim(),
                Color = TagColorEntry.Text?.Trim()
            });
            if (result.Success)
            {
                EditorPanel.IsVisible = false;
                await LoadTagsAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create tag", "OK");
            }
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadTagsAsync();
        RefreshContainer.IsRefreshing = false;
    }
}
