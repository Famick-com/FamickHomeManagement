using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Contacts;

public partial class SelectHouseholdPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Timer? _searchDebounceTimer;
    private string _currentSearchTerm = string.Empty;
    private List<HouseholdDisplayItem> _allHouseholds = new();

    public SelectHouseholdPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHouseholdsAsync();
    }

    private async Task LoadHouseholdsAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ErrorFrame.IsVisible = false;
            EmptyState.IsVisible = false;
            HouseholdsList.IsVisible = false;
        });

        try
        {
            var result = await _apiClient.GetContactGroupsAsync(
                searchTerm: string.IsNullOrWhiteSpace(_currentSearchTerm) ? null : _currentSearchTerm,
                contactType: 0, // Households only
                pageSize: 100);

            if (!result.Success)
            {
                ShowError(result.ErrorMessage ?? "Failed to load households.");
                return;
            }

            var items = result.Data?.Items ?? new List<ContactGroupSummaryDto>();
            _allHouseholds = items.Select(g => new HouseholdDisplayItem(g)).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_allHouseholds.Count == 0)
                {
                    EmptyState.IsVisible = true;
                    HouseholdsList.IsVisible = false;
                }
                else
                {
                    HouseholdsList.ItemsSource = _allHouseholds;
                    HouseholdsList.IsVisible = true;
                }

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            });
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new Timer(_ =>
        {
            _currentSearchTerm = e.NewTextValue ?? string.Empty;
            _ = LoadHouseholdsAsync();
        }, null, 400, Timeout.Infinite);
    }

    private async void OnHouseholdSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not HouseholdDisplayItem selected)
            return;

        // Clear selection for next time
        HouseholdsList.SelectedItem = null;

        // Navigate back and trigger import on the ImportContactPage
        // We use a query parameter to pass the selected household ID
        var pages = Shell.Current.Navigation.NavigationStack;
        var importPage = pages.OfType<ImportContactPage>().LastOrDefault();

        if (importPage != null)
        {
            await Shell.Current.GoToAsync("..");
            await importPage.HandleHouseholdSelected(selected.Id);
        }
        else
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            HouseholdsList.IsVisible = false;
            EmptyState.IsVisible = false;
            ErrorFrame.IsVisible = true;
            ErrorLabel.Text = message;
        });
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadHouseholdsAsync();
    }
}

public class HouseholdDisplayItem
{
    private readonly ContactGroupSummaryDto _dto;

    public HouseholdDisplayItem(ContactGroupSummaryDto dto) => _dto = dto;

    public Guid Id => _dto.Id;
    public string GroupName => _dto.GroupName;
    public string? PrimaryAddress => _dto.PrimaryAddress;
    public int MemberCount => _dto.MemberCount;
    public string Initials
    {
        get
        {
            var words = GroupName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
                return $"{words[0][0]}{words[1][0]}".ToUpperInvariant();
            return words.Length > 0 ? words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant() : "?";
        }
    }
}
