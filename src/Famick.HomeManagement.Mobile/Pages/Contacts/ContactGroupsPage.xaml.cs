using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using Syncfusion.Maui.Popup;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

public partial class ContactGroupsPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private int? _currentTypeFilter; // null=All, 0=Household, 1=Business
    private ContactSortOrder _currentSortOrder = ContactSortOrder.Name;

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
                    var items = result.Data.Items.ToList();

                    // Apply client-side sorting
                    items = _currentSortOrder switch
                    {
                        ContactSortOrder.DateCreated => items.OrderByDescending(g => g.CreatedAt).ToList(),
                        ContactSortOrder.MemberCount => items.OrderByDescending(g => g.MemberCount).ToList(),
                        _ => items.OrderBy(g => g.GroupName).ToList()
                    };

                    Groups.Clear();
                    foreach (var group in items)
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

    private void OnFilterClicked(object? sender, EventArgs e)
    {
        FilterPopup.ContentTemplate = new DataTemplate(() => BuildFilterContent());
        FilterPopup.ShowRelativeToView(FilterAnchor, PopupRelativePosition.AlignBottomLeft);
    }

    private View BuildFilterContent()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var headerColor = isDark ? Color.FromArgb("#757575") : Color.FromArgb("#9E9E9E");
        var dividerColor = isDark ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");

        var container = new VerticalStackLayout { Spacing = 0, WidthRequest = 240 };

        // Contact Type section
        container.Children.Add(new Label
        {
            Text = "Contact Type",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(16, 14, 16, 6),
            TextColor = headerColor
        });
        AddFilterOption(container, "All", _currentTypeFilter == null, () => ApplyTypeFilter(null));
        AddFilterOption(container, "Households", _currentTypeFilter == 0, () => ApplyTypeFilter(0));
        AddFilterOption(container, "Businesses", _currentTypeFilter == 1, () => ApplyTypeFilter(1));

        container.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Margin = new Thickness(16, 4),
            Color = dividerColor
        });

        // Sort By section
        container.Children.Add(new Label
        {
            Text = "Sort By",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(16, 8, 16, 6),
            TextColor = headerColor
        });
        AddFilterOption(container, "Name", _currentSortOrder == ContactSortOrder.Name, () => ApplySortOrder(ContactSortOrder.Name));
        AddFilterOption(container, "Date Created", _currentSortOrder == ContactSortOrder.DateCreated, () => ApplySortOrder(ContactSortOrder.DateCreated));
        AddFilterOption(container, "Member Count", _currentSortOrder == ContactSortOrder.MemberCount, () => ApplySortOrder(ContactSortOrder.MemberCount));

        container.Children.Add(new BoxView { HeightRequest = 8, Color = Colors.Transparent });

        return container;
    }

    private void AddFilterOption(Layout container, string label, bool isSelected, Action onTap)
    {
        var grid = new Grid
        {
            Padding = new Thickness(16, 11),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        var textLabel = new Label
        {
            Text = label,
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(textLabel, 0);

        var checkLabel = new Label
        {
            Text = isSelected ? "\u2713" : "",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1976D2"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(checkLabel, 1);

        grid.Children.Add(textLabel);
        grid.Children.Add(checkLabel);

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (_, _) => onTap();
        grid.GestureRecognizers.Add(tapGesture);

        container.Children.Add(grid);
    }

    private void ApplyTypeFilter(int? type)
    {
        _currentTypeFilter = type;
        FilterPopup.IsOpen = false;
        _ = LoadGroupsAsync();
    }

    private void ApplySortOrder(ContactSortOrder sort)
    {
        _currentSortOrder = sort;
        FilterPopup.IsOpen = false;
        _ = LoadGroupsAsync();
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

    private async void OnImportFromDeviceClicked(object? sender, EventArgs e)
    {
        var picker = Handler?.MauiContext?.Services.GetService<IDeviceContactPicker>();
        if (picker == null) return;

        var contactData = await picker.PickContactAsync();
        if (contactData == null) return;

        App.PendingSharedContact = contactData;
        await Shell.Current.GoToAsync(nameof(ImportContactPage));
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
