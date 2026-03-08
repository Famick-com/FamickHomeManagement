using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Equipment;

[QueryProperty(nameof(EquipmentId), "EquipmentId")]
public partial class EquipmentEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private EquipmentDetailItem? _equipment;
    private bool _isEditMode;
    private bool _loaded;
    private List<EquipmentCategoryItem> _categories = new();

    public string EquipmentId { get; set; } = string.Empty;

    public EquipmentEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        PurchaseDatePicker.Date = DateTime.Now.Date;
        WarrantyDatePicker.Date = DateTime.Now.Date.AddYears(1);
        PurchaseDatePicker.IsEnabled = false;
        WarrantyDatePicker.IsEnabled = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded) return;
        _loaded = true;

        _isEditMode = !string.IsNullOrEmpty(EquipmentId) && Guid.TryParse(EquipmentId, out _);

        await LoadCategoriesAsync();

        if (_isEditMode)
        {
            TitleLabel.Text = "Edit Equipment";
            await LoadEquipmentAsync();
        }
        else
        {
            TitleLabel.Text = "New Equipment";
        }
    }

    private async Task LoadCategoriesAsync()
    {
        var result = await _apiClient.GetEquipmentCategoriesAsync();
        if (result.Success && result.Data != null)
        {
            _categories = result.Data;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var names = new List<string> { "(None)" };
                names.AddRange(_categories.Select(c => c.Name));
                CategoryPicker.ItemsSource = names;
                CategoryPicker.SelectedIndex = 0;
            });
        }
    }

    private async Task LoadEquipmentAsync()
    {
        if (!Guid.TryParse(EquipmentId, out var id)) return;

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;

        try
        {
            var result = await _apiClient.GetEquipmentAsync(id);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _equipment = result.Data;
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
                _ = DisplayAlert("Error", $"Failed to load equipment: {ex.Message}", "OK");
            });
        }
    }

    private void PopulateForm()
    {
        if (_equipment == null) return;

        NameEntry.Text = _equipment.Name;
        DescriptionEditor.Text = _equipment.Description;
        LocationEntry.Text = _equipment.Location;
        ManufacturerEntry.Text = _equipment.Manufacturer;
        ManufacturerLinkEntry.Text = _equipment.ManufacturerLink;
        ModelNumberEntry.Text = _equipment.ModelNumber;
        SerialNumberEntry.Text = _equipment.SerialNumber;
        PurchaseLocationEntry.Text = _equipment.PurchaseLocation;
        WarrantyContactEntry.Text = _equipment.WarrantyContactInfo;
        UsageUnitEntry.Text = _equipment.UsageUnit;
        NotesEditor.Text = _equipment.Notes;

        // Category
        if (_equipment.CategoryId.HasValue)
        {
            var catIndex = _categories.FindIndex(c => c.Id == _equipment.CategoryId.Value);
            CategoryPicker.SelectedIndex = catIndex >= 0 ? catIndex + 1 : 0; // +1 for "(None)"
        }

        // Purchase date
        if (_equipment.PurchaseDate.HasValue)
        {
            HasPurchaseDateSwitch.IsToggled = true;
            PurchaseDatePicker.IsEnabled = true;
            PurchaseDatePicker.Date = _equipment.PurchaseDate.Value.ToLocalTime().Date;
        }

        // Warranty date
        if (_equipment.WarrantyExpirationDate.HasValue)
        {
            HasWarrantyDateSwitch.IsToggled = true;
            WarrantyDatePicker.IsEnabled = true;
            WarrantyDatePicker.Date = _equipment.WarrantyExpirationDate.Value.ToLocalTime().Date;
        }
    }

    private void OnHasPurchaseDateToggled(object? sender, ToggledEventArgs e)
    {
        PurchaseDatePicker.IsEnabled = e.Value;
    }

    private void OnHasWarrantyDateToggled(object? sender, ToggledEventArgs e)
    {
        WarrantyDatePicker.IsEnabled = e.Value;
    }

    private async void OnNewCategoryClicked(object? sender, EventArgs e)
    {
        var popup = new EquipmentCategoryPopup();
        var result = await this.ShowPopupAsync<EquipmentCategoryPopupResult>(popup, PopupOptions.Empty, CancellationToken.None);

        if (result is EquipmentCategoryPopupResult categoryResult)
        {
            var apiResult = await _apiClient.CreateEquipmentCategoryAsync(new CreateEquipmentCategoryMobileRequest
            {
                Name = categoryResult.Name,
                Description = categoryResult.Description
            });

            if (apiResult.Success && apiResult.Data != null)
            {
                _categories.Add(apiResult.Data);
                var names = new List<string> { "(None)" };
                names.AddRange(_categories.Select(c => c.Name));
                CategoryPicker.ItemsSource = names;
                CategoryPicker.SelectedIndex = names.Count - 1; // Select newly created
            }
            else
            {
                await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to create category", "OK");
            }
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Validation", "Equipment name is required.", "OK");
            return;
        }

        Guid? categoryId = null;
        if (CategoryPicker.SelectedIndex > 0)
        {
            categoryId = _categories[CategoryPicker.SelectedIndex - 1].Id;
        }

        DateTime? purchaseDate = null;
        if (HasPurchaseDateSwitch.IsToggled && PurchaseDatePicker.Date is { } purchDate)
        {
            purchaseDate = new DateTime(purchDate.Year, purchDate.Month, purchDate.Day,
                0, 0, 0, DateTimeKind.Local).ToUniversalTime();
        }

        DateTime? warrantyDate = null;
        if (HasWarrantyDateSwitch.IsToggled && WarrantyDatePicker.Date is { } warDate)
        {
            warrantyDate = new DateTime(warDate.Year, warDate.Month, warDate.Day,
                0, 0, 0, DateTimeKind.Local).ToUniversalTime();
        }

        SaveToolbarItem.IsEnabled = false;

        try
        {
            if (_isEditMode && _equipment != null)
            {
                var request = new UpdateEquipmentMobileRequest
                {
                    Name = name,
                    Description = DescriptionEditor.Text?.Trim(),
                    Location = LocationEntry.Text?.Trim(),
                    Manufacturer = ManufacturerEntry.Text?.Trim(),
                    ManufacturerLink = ManufacturerLinkEntry.Text?.Trim(),
                    ModelNumber = ModelNumberEntry.Text?.Trim(),
                    SerialNumber = SerialNumberEntry.Text?.Trim(),
                    PurchaseDate = purchaseDate,
                    PurchaseLocation = PurchaseLocationEntry.Text?.Trim(),
                    WarrantyExpirationDate = warrantyDate,
                    WarrantyContactInfo = WarrantyContactEntry.Text?.Trim(),
                    UsageUnit = UsageUnitEntry.Text?.Trim(),
                    Notes = NotesEditor.Text?.Trim(),
                    CategoryId = categoryId,
                    ParentEquipmentId = _equipment.ParentEquipmentId
                };

                var result = await _apiClient.UpdateEquipmentAsync(_equipment.Id, request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update equipment", "OK");
            }
            else
            {
                var request = new CreateEquipmentMobileRequest
                {
                    Name = name,
                    Description = DescriptionEditor.Text?.Trim(),
                    Location = LocationEntry.Text?.Trim(),
                    Manufacturer = ManufacturerEntry.Text?.Trim(),
                    ManufacturerLink = ManufacturerLinkEntry.Text?.Trim(),
                    ModelNumber = ModelNumberEntry.Text?.Trim(),
                    SerialNumber = SerialNumberEntry.Text?.Trim(),
                    PurchaseDate = purchaseDate,
                    PurchaseLocation = PurchaseLocationEntry.Text?.Trim(),
                    WarrantyExpirationDate = warrantyDate,
                    WarrantyContactInfo = WarrantyContactEntry.Text?.Trim(),
                    UsageUnit = UsageUnitEntry.Text?.Trim(),
                    Notes = NotesEditor.Text?.Trim(),
                    CategoryId = categoryId
                };

                var result = await _apiClient.CreateEquipmentAsync(request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create equipment", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

        SaveToolbarItem.IsEnabled = true;
    }
}
