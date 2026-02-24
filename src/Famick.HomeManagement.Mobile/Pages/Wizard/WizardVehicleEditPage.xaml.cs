using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Wizard;

public partial class WizardVehicleEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly VehicleSummaryDto? _existingVehicle;
    private readonly List<HouseholdMemberDto> _members;

    public WizardVehicleEditPage(
        ShoppingApiClient apiClient,
        VehicleSummaryDto? existingVehicle,
        List<HouseholdMemberDto> members)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _existingVehicle = existingVehicle;
        _members = members;

        SetupDriverPicker();

        if (_existingVehicle != null)
        {
            PageTitle.Text = "Edit Vehicle";
            PopulateExistingData();
        }
    }

    private void SetupDriverPicker()
    {
        var driverNames = _members.Select(m => m.DisplayName).ToList();
        driverNames.Insert(0, "(None)");
        PrimaryDriverPicker.ItemsSource = driverNames;
        PrimaryDriverPicker.SelectedIndex = 0;
    }

    private void PopulateExistingData()
    {
        if (_existingVehicle == null) return;

        YearEntry.Text = _existingVehicle.Year.ToString();
        MakeEntry.Text = _existingVehicle.Make;
        ModelEntry.Text = _existingVehicle.Model;
        TrimEntry.Text = _existingVehicle.Trim;
        ColorEntry.Text = _existingVehicle.Color;
        LicensePlateEntry.Text = _existingVehicle.LicensePlate;
        MileageEntry.Text = _existingVehicle.CurrentMileage?.ToString();

        if (!string.IsNullOrEmpty(_existingVehicle.PrimaryDriverName))
        {
            var idx = _members.FindIndex(m => m.DisplayName == _existingVehicle.PrimaryDriverName);
            if (idx >= 0)
            {
                PrimaryDriverPicker.SelectedIndex = idx + 1; // +1 for "(None)" at index 0
            }
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var yearText = YearEntry.Text?.Trim();
        var make = MakeEntry.Text?.Trim();
        var model = ModelEntry.Text?.Trim();

        if (string.IsNullOrEmpty(yearText) || string.IsNullOrEmpty(make) || string.IsNullOrEmpty(model))
        {
            ShowError("Year, Make, and Model are required.");
            return;
        }

        if (!int.TryParse(yearText, out var year) || year < 1900 || year > DateTime.Now.Year + 2)
        {
            ShowError("Please enter a valid year.");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            Guid? driverContactId = null;
            if (PrimaryDriverPicker.SelectedIndex > 0)
            {
                driverContactId = _members[PrimaryDriverPicker.SelectedIndex - 1].ContactId;
            }

            int? mileage = int.TryParse(MileageEntry.Text, out var m) ? m : null;

            if (_existingVehicle != null)
            {
                var request = new UpdateVehicleRequest
                {
                    Year = year,
                    Make = make,
                    Model = model,
                    Trim = TrimEntry.Text?.Trim(),
                    Color = ColorEntry.Text?.Trim(),
                    Vin = VinEntry.Text?.Trim(),
                    LicensePlate = LicensePlateEntry.Text?.Trim(),
                    CurrentMileage = mileage,
                    PrimaryDriverContactId = driverContactId,
                    IsActive = true
                };
                var result = await _apiClient.UpdateVehicleAsync(_existingVehicle.Id, request);
                if (!result.Success)
                {
                    ShowError(result.ErrorMessage ?? "Failed to update vehicle.");
                    return;
                }
            }
            else
            {
                var request = new CreateVehicleRequest
                {
                    Year = year,
                    Make = make,
                    Model = model,
                    Trim = TrimEntry.Text?.Trim(),
                    Color = ColorEntry.Text?.Trim(),
                    Vin = VinEntry.Text?.Trim(),
                    LicensePlate = LicensePlateEntry.Text?.Trim(),
                    CurrentMileage = mileage,
                    PrimaryDriverContactId = driverContactId
                };
                var result = await _apiClient.CreateVehicleAsync(request);
                if (!result.Success)
                {
                    ShowError(result.ErrorMessage ?? "Failed to create vehicle.");
                    return;
                }
            }

            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void SetLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;
        });
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        });
    }

    private void HideError()
    {
        MainThread.BeginInvokeOnMainThread(() => ErrorLabel.IsVisible = false);
    }
}
