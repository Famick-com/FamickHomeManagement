using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class ForceChangePasswordPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly TokenStorage _tokenStorage;

    // Email passed from login flow for re-authentication
    public string? UserEmail { get; set; }

    public ForceChangePasswordPage(
        ShoppingApiClient apiClient,
        TokenStorage tokenStorage)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
    }

    private async void OnChangePasswordClicked(object? sender, EventArgs e)
    {
        var currentPassword = CurrentPasswordEntry.Text?.Trim();
        var newPassword = NewPasswordEntry.Text?.Trim();
        var confirmPassword = ConfirmPasswordEntry.Text?.Trim();

        // Validate inputs
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            ShowError("Please enter your current password");
            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ShowError("Please enter a new password");
            return;
        }

        if (newPassword.Length < 8)
        {
            ShowError("Password must be at least 8 characters");
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowError("Passwords do not match");
            return;
        }

        SetLoading(true);
        HideError();

        try
        {
            var result = await _apiClient.ChangePasswordAsync(currentPassword, newPassword, confirmPassword);

            if (result.Success)
            {
                SuccessLabel.Text = "Password changed successfully. Signing you in...";
                SuccessLabel.IsVisible = true;

                // Re-login with new password to get a fresh JWT
                if (!string.IsNullOrEmpty(UserEmail))
                {
                    var loginResult = await _apiClient.LoginAsync(UserEmail, newPassword);
                    if (loginResult.Success && loginResult.Data != null)
                    {
                        await _tokenStorage.SetTokensAsync(loginResult.Data.AccessToken, loginResult.Data.RefreshToken);
                        await Task.Delay(1000);
                        await Shell.Current.GoToAsync("//DashboardPage");
                        return;
                    }
                }

                // If re-login fails, clear tokens and go to login
                await _tokenStorage.ClearTokensAsync();
                await Task.Delay(1000);
                await Shell.Current.GoToAsync("//LoginPage");
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Failed to change password. Please try again.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Connection error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        ChangePasswordButton.IsEnabled = !isLoading;
        CurrentPasswordEntry.IsEnabled = !isLoading;
        NewPasswordEntry.IsEnabled = !isLoading;
        ConfirmPasswordEntry.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
        SuccessLabel.IsVisible = false;
    }

    private void HideError()
    {
        ErrorLabel.IsVisible = false;
    }
}
