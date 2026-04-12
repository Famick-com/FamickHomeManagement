using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Household;

public partial class HouseholdOverviewEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private MobileHomeDto? _home;
    private Guid? _householdContactId;
    private bool _loaded;

    public HouseholdOverviewEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        try
        {
            var homeTask = _apiClient.GetHomeAsync();
            var householdTask = _apiClient.GetHouseholdContactAsync();
            await Task.WhenAll(homeTask, householdTask);

            var result = homeTask.Result;
            var householdResult = householdTask.Result;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _home = result.Data;
                    PopulateForm();
                }

                if (householdResult.Success && householdResult.Data != null)
                {
                    _householdContactId = householdResult.Data.Id;
                    _ = LoadProfileImageAsync(householdResult.Data.ProfileImageUrl);
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
                _ = DisplayAlert("Error", $"Failed to load: {ex.Message}", "OK");
            });
        }
    }

    private async Task LoadProfileImageAsync(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;

        var source = await _apiClient.LoadImageAsync(imageUrl);
        if (source != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AvatarView.ImageSource = source;
                AvatarView.ContentType = Syncfusion.Maui.Core.ContentType.Custom;
            });
        }
    }

    private async void OnProfileImageTapped(object? sender, EventArgs e)
    {
        if (_householdContactId == null) return;

        var hasImage = AvatarView.ImageSource != null;
        var options = hasImage
            ? new[] { "Take Photo", "Choose from Gallery", "Remove Image" }
            : new[] { "Take Photo", "Choose from Gallery" };

        var action = await DisplayActionSheet("Household Photo", "Cancel", null, options);

        switch (action)
        {
            case "Take Photo":
                await CaptureAndUploadImageAsync(true);
                break;
            case "Choose from Gallery":
                await CaptureAndUploadImageAsync(false);
                break;
            case "Remove Image":
                var result = await _apiClient.DeleteContactProfileImageAsync(_householdContactId.Value);
                if (result.Success)
                {
                    AvatarView.ImageSource = null;
                    AvatarView.ContentType = Syncfusion.Maui.Core.ContentType.Initials;
                    WeakReferenceMessenger.Default.Send(new HouseholdProfileImageChangedMessage());
                }
                break;
        }
    }

    private async Task CaptureAndUploadImageAsync(bool useCamera)
    {
        if (_householdContactId == null) return;

        try
        {
            FileResult? photo;
            if (useCamera)
            {
                photo = await MediaPicker.Default.CapturePhotoAsync();
            }
            else
            {
                photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select Household Photo"
                });
            }

            if (photo == null) return;

            var stream = await photo.OpenReadAsync();
            var cropPage = new Profile.ProfileImageCropPage(stream);
            await Navigation.PushModalAsync(new NavigationPage(cropPage));
            var croppedStream = await cropPage.CropResultTask;
            if (croppedStream == null) return;

            var result = await _apiClient.UploadContactProfileImageAsync(
                _householdContactId.Value, croppedStream, "profile.png");

            if (result.Success)
            {
                // Reload the household to get the new image URL
                var household = await _apiClient.GetHouseholdContactAsync();
                if (household.Success && household.Data != null)
                {
                    await LoadProfileImageAsync(household.Data.ProfileImageUrl);
                }
                WeakReferenceMessenger.Default.Send(new HouseholdProfileImageChangedMessage());
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to upload image", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to access camera/gallery: {ex.Message}", "OK");
        }
    }

    private void PopulateForm()
    {
        if (_home == null) return;
        UnitEntry.Text = _home.Unit;
        YearBuiltEntry.Text = _home.YearBuilt?.ToString();
        SqFtEntry.Text = _home.SquareFootage?.ToString();
        BedroomsEntry.Text = _home.Bedrooms?.ToString();
        BathroomsEntry.Text = _home.Bathrooms?.ToString("0.#");
        HoaNameEntry.Text = _home.HoaName;
        HoaContactEntry.Text = _home.HoaContactInfo;
        HoaRulesEntry.Text = _home.HoaRulesLink;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        SaveToolbarItem.IsEnabled = false;

        try
        {
            var request = BuildRequest();
            var result = await _apiClient.UpdateHomeAsync(request);

            if (result.Success)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to save", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

        SaveToolbarItem.IsEnabled = true;
    }

    private UpdateHomeMobileRequest BuildRequest()
    {
        int.TryParse(YearBuiltEntry.Text, out var yearBuilt);
        int.TryParse(SqFtEntry.Text, out var sqFt);
        int.TryParse(BedroomsEntry.Text, out var bedrooms);
        decimal.TryParse(BathroomsEntry.Text, out var bathrooms);

        return new UpdateHomeMobileRequest
        {
            // Overview fields being edited
            Unit = UnitEntry.Text?.Trim(),
            YearBuilt = yearBuilt > 0 ? yearBuilt : null,
            SquareFootage = sqFt > 0 ? sqFt : null,
            Bedrooms = bedrooms > 0 ? bedrooms : null,
            Bathrooms = bathrooms > 0 ? bathrooms : null,
            HoaName = HoaNameEntry.Text?.Trim(),
            HoaContactInfo = HoaContactEntry.Text?.Trim(),
            HoaRulesLink = HoaRulesEntry.Text?.Trim(),
            // Pass through existing values for other sections
            AcFilterSizes = _home?.AcFilterSizes,
            AcFilterReplacementIntervalDays = _home?.AcFilterReplacementIntervalDays,
            FridgeWaterFilterType = _home?.FridgeWaterFilterType,
            UnderSinkFilterType = _home?.UnderSinkFilterType,
            WholeHouseFilterType = _home?.WholeHouseFilterType,
            SmokeCoDetectorBatteryType = _home?.SmokeCoDetectorBatteryType,
            HvacServiceSchedule = _home?.HvacServiceSchedule,
            PestControlSchedule = _home?.PestControlSchedule,
            InsuranceType = _home?.InsuranceType,
            InsurancePolicyNumber = _home?.InsurancePolicyNumber,
            InsuranceAgentName = _home?.InsuranceAgentName,
            InsuranceAgentPhone = _home?.InsuranceAgentPhone,
            InsuranceAgentEmail = _home?.InsuranceAgentEmail,
            MortgageInfo = _home?.MortgageInfo,
            PropertyTaxAccountNumber = _home?.PropertyTaxAccountNumber,
            EscrowDetails = _home?.EscrowDetails,
            AppraisalValue = _home?.AppraisalValue,
            AppraisalDate = _home?.AppraisalDate
        };
    }
}
