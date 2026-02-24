using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Wizard;

public partial class WizardHouseholdInfoPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly OnboardingService _onboardingService;
    private NormalizedAddressResult? _normalizedAddress;
    private bool _addressVerified;

    public WizardHouseholdInfoPage(ShoppingApiClient apiClient, OnboardingService onboardingService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _onboardingService = onboardingService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadExistingDataAsync();
    }

    private async Task LoadExistingDataAsync()
    {
        SetLoading(true);
        try
        {
            var result = await _apiClient.GetWizardStateAsync();
            if (result.Success && result.Data != null)
            {
                var info = result.Data.HouseholdInfo;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    HouseholdNameEntry.Text = info.Name;
                    Street1Entry.Text = info.Street1;
                    Street2Entry.Text = info.Street2;
                    CityEntry.Text = info.City;
                    StateEntry.Text = info.State;
                    PostalCodeEntry.Text = info.PostalCode;
                    CountryEntry.Text = info.Country ?? "US";

                    if (info.IsAddressNormalized)
                    {
                        _addressVerified = true;
                    }
                });
            }
        }
        catch
        {
            // Pre-fill failed, user can still enter data
        }
        finally
        {
            SetLoading(false);
        }
    }

    private bool HasAddressContent()
    {
        return !string.IsNullOrWhiteSpace(Street1Entry.Text) ||
               !string.IsNullOrWhiteSpace(CityEntry.Text) ||
               !string.IsNullOrWhiteSpace(PostalCodeEntry.Text);
    }

    private async void OnVerifyAddressClicked(object? sender, EventArgs e)
    {
        if (!HasAddressContent())
        {
            ShowError("Please enter an address to verify.");
            return;
        }

        await NormalizeAddressAsync();
    }

    private async Task NormalizeAddressAsync()
    {
        SetLoading(true);
        HideError();
        try
        {
            var request = new NormalizeAddressRequest
            {
                AddressLine1 = Street1Entry.Text?.Trim(),
                AddressLine2 = Street2Entry.Text?.Trim(),
                City = CityEntry.Text?.Trim(),
                StateProvince = StateEntry.Text?.Trim(),
                PostalCode = PostalCodeEntry.Text?.Trim(),
                Country = CountryEntry.Text?.Trim()
            };

            var result = await _apiClient.NormalizeAddressAsync(request);
            if (result.Success && result.Data != null)
            {
                _normalizedAddress = result.Data;

                // If fallback (no geocoding API key) or exact match, auto-accept
                if (_normalizedAddress.MatchType == "fallback" || IsExactMatch(_normalizedAddress))
                {
                    _addressVerified = true;
                    MainThread.BeginInvokeOnMainThread(() => NormalizedAddressBorder.IsVisible = false);
                }
                else
                {
                    // Show the normalized suggestion for user confirmation
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var formatted = _normalizedAddress.FormattedAddress
                            ?? FormatAddress(_normalizedAddress);
                        NormalizedAddressLabel.Text = formatted;
                        NormalizedAddressBorder.IsVisible = true;
                    });
                }
            }
            else
            {
                // Normalization failed or not available - allow proceeding without it
                _addressVerified = true;
                ShowError(result.ErrorMessage ?? "Address verification not available. You can continue.");
            }
        }
        catch (Exception ex)
        {
            _addressVerified = true; // Don't block on normalization failure
            ShowError($"Address verification error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private bool IsExactMatch(NormalizedAddressResult normalized)
    {
        return string.Equals(normalized.AddressLine1?.Trim(), Street1Entry.Text?.Trim(), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(normalized.City?.Trim(), CityEntry.Text?.Trim(), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(normalized.StateProvince?.Trim(), StateEntry.Text?.Trim(), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(normalized.PostalCode?.Trim(), PostalCodeEntry.Text?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatAddress(NormalizedAddressResult addr)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(addr.AddressLine1)) parts.Add(addr.AddressLine1);
        if (!string.IsNullOrWhiteSpace(addr.AddressLine2)) parts.Add(addr.AddressLine2);

        var cityLine = string.Join(", ",
            new[] { addr.City, addr.StateProvince }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(addr.PostalCode))
            cityLine = string.IsNullOrEmpty(cityLine) ? addr.PostalCode : $"{cityLine} {addr.PostalCode}";
        if (!string.IsNullOrWhiteSpace(cityLine)) parts.Add(cityLine);

        return string.Join("\n", parts);
    }

    private void OnUseNormalizedClicked(object? sender, EventArgs e)
    {
        if (_normalizedAddress == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Street1Entry.Text = _normalizedAddress.AddressLine1;
            Street2Entry.Text = _normalizedAddress.AddressLine2;
            CityEntry.Text = _normalizedAddress.City;
            StateEntry.Text = _normalizedAddress.StateProvince;
            PostalCodeEntry.Text = _normalizedAddress.PostalCode;
            CountryEntry.Text = _normalizedAddress.Country ?? _normalizedAddress.CountryCode ?? "US";
            NormalizedAddressBorder.IsVisible = false;
        });
        _addressVerified = true;
    }

    private void OnKeepOriginalClicked(object? sender, EventArgs e)
    {
        _addressVerified = true;
        MainThread.BeginInvokeOnMainThread(() => NormalizedAddressBorder.IsVisible = false);
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        var name = HouseholdNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ShowError("Please enter a household name.");
            return;
        }

        // Auto-normalize if address present but not yet verified
        if (HasAddressContent() && !_addressVerified)
        {
            await NormalizeAddressAsync();
            // If normalization showed a dialog, wait for user to confirm before proceeding
            if (NormalizedAddressBorder.IsVisible)
                return;
        }

        SetLoading(true);
        HideError();

        try
        {
            var dto = new HouseholdInfoDto
            {
                Name = name,
                Street1 = Street1Entry.Text?.Trim(),
                Street2 = Street2Entry.Text?.Trim(),
                City = CityEntry.Text?.Trim(),
                State = StateEntry.Text?.Trim(),
                PostalCode = PostalCodeEntry.Text?.Trim(),
                Country = CountryEntry.Text?.Trim(),
                IsAddressNormalized = _addressVerified
            };

            var result = await _apiClient.SaveHouseholdInfoAsync(dto);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage ?? "Failed to save.");
                return;
            }

            await Navigation.PushAsync(
                Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<WizardMembersPage>());
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
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.IsVisible = false;
        });
    }
}
