using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

public partial class ContactGroupsPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private int? _currentTypeFilter; // null=All, 0=Household, 1=Business

    public ObservableCollection<ContactGroupDisplayModel> Groups { get; } = new();

    public ContactGroupsPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadGroupsAsync().ConfigureAwait(false);
    }

    private async Task LoadGroupsAsync()
    {
        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var result = await _apiClient.GetContactGroupsAsync(
                _currentSearchTerm, _currentTypeFilter);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    Groups.Clear();
                    foreach (var group in result.Data.Items)
                    {
                        Groups.Add(new ContactGroupDisplayModel(group));
                    }

                    if (Groups.Count == 0)
                        ShowEmpty();
                    else
                        ShowContent();
                }
                else
                {
                    ShowError(result.ErrorMessage ?? "Failed to load groups");
                }
            });

            // Load profile images in background
            if (result.Success && result.Data != null)
                _ = LoadProfileImagesAsync();
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError($"Connection error: {ex.Message}"));
        }
    }

    private async Task LoadProfileImagesAsync()
    {
        foreach (var group in Groups.ToList())
        {
            if (string.IsNullOrEmpty(group.ProfileImageUrl)) continue;

            var source = await _apiClient.LoadImageAsync(group.ProfileImageUrl);
            if (source != null)
                MainThread.BeginInvokeOnMainThread(() => group.ProfileImageSource = source);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new Timer(_ =>
        {
            _currentSearchTerm = e.NewTextValue ?? string.Empty;
            _ = LoadGroupsAsync();
        }, null, 400, Timeout.Infinite);
    }

    private void OnFilterAllClicked(object? sender, EventArgs e)
    {
        _currentTypeFilter = null;
        UpdateFilterChips();
        _ = LoadGroupsAsync();
    }

    private void OnFilterHouseholdsClicked(object? sender, EventArgs e)
    {
        _currentTypeFilter = 0;
        UpdateFilterChips();
        _ = LoadGroupsAsync();
    }

    private void OnFilterBusinessesClicked(object? sender, EventArgs e)
    {
        _currentTypeFilter = 1;
        UpdateFilterChips();
        _ = LoadGroupsAsync();
    }

    private void UpdateFilterChips()
    {
        var activeColor = Color.FromArgb("#1976D2");
        var inactiveLight = Color.FromArgb("#E0E0E0");
        var inactiveDark = Color.FromArgb("#424242");
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        var inactive = isLight ? inactiveLight : inactiveDark;

        FilterAll.BackgroundColor = _currentTypeFilter == null ? activeColor : inactive;
        FilterAll.TextColor = _currentTypeFilter == null ? Colors.White : (isLight ? Colors.Black : Colors.White);
        FilterHouseholds.BackgroundColor = _currentTypeFilter == 0 ? activeColor : inactive;
        FilterHouseholds.TextColor = _currentTypeFilter == 0 ? Colors.White : (isLight ? Colors.Black : Colors.White);
        FilterBusinesses.BackgroundColor = _currentTypeFilter == 1 ? activeColor : inactive;
        FilterBusinesses.TextColor = _currentTypeFilter == 1 ? Colors.White : (isLight ? Colors.Black : Colors.White);
    }

    private async void OnGroupSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ContactGroupDisplayModel selected)
            return;

        GroupCollection.SelectedItem = null;

        await Shell.Current.GoToAsync(nameof(ContactGroupDetailPage), new Dictionary<string, object>
        {
            { "GroupId", selected.Id.ToString() }
        });
    }

    private async void OnAddGroupClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ContactGroupEditPage), new Dictionary<string, object>
        {
            { "GroupId", string.Empty }
        });
    }

    private async void OnEditSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactGroupDisplayModel group })
        {
            await Shell.Current.GoToAsync(nameof(ContactGroupEditPage), new Dictionary<string, object>
            {
                { "GroupId", group.Id.ToString() }
            });
        }
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: ContactGroupDisplayModel group })
        {
            if (group.IsTenantHousehold)
            {
                await DisplayAlert("Cannot Delete", "You cannot delete the household group.", "OK");
                return;
            }

            var typeLabel = group.TypeLabel == "Business" ? "Business" : "Household";
            var confirm = await DisplayAlert($"Delete {typeLabel}",
                $"Are you sure you want to delete \"{group.GroupName}\"?", "Delete", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.DeleteContactGroupAsync(group.Id);
            if (result.Success)
            {
                Groups.Remove(group);
                if (Groups.Count == 0) ShowEmpty();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete group", "OK");
            }
        }
    }

    private void OnRefreshClicked(object? sender, EventArgs e)
    {
        _ = LoadGroupsAsync();
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        RetryButton.IsEnabled = false;
        RetryButton.Text = "Retrying...";
        try { await LoadGroupsAsync(); }
        finally { RetryButton.IsEnabled = true; RetryButton.Text = "Retry"; }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadGroupsAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        RefreshContainer.IsVisible = false;
        EmptyState.IsVisible = false;
        ErrorFrame.IsVisible = false;
    }

    private void ShowContent()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        RefreshContainer.IsVisible = true;
        EmptyState.IsVisible = false;
        ErrorFrame.IsVisible = false;
    }

    private void ShowEmpty()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        RefreshContainer.IsVisible = false;
        EmptyState.IsVisible = true;
        ErrorFrame.IsVisible = false;
    }

    private void ShowError(string message)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        RefreshContainer.IsVisible = false;
        EmptyState.IsVisible = false;
        ErrorFrame.IsVisible = true;
        ErrorLabel.Text = message;
    }
}
