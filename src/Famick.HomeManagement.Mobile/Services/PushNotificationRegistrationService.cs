namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Orchestrates push notification registration lifecycle:
/// registers device token after login, unregisters on sign-out,
/// and handles automatic re-registration on token refresh.
/// </summary>
public class PushNotificationRegistrationService
{
    private const string StorageKeyTokenId = "push_device_token_id";

    private readonly IPushTokenProvider _tokenProvider;
    private readonly ShoppingApiClient _apiClient;

    public PushNotificationRegistrationService(
        IPushTokenProvider tokenProvider,
        ShoppingApiClient apiClient)
    {
        _tokenProvider = tokenProvider;
        _apiClient = apiClient;

        // Subscribe to token refresh events for automatic re-registration
        _tokenProvider.TokenRefreshed += OnTokenRefreshed;
    }

    /// <summary>
    /// Requests push permission, gets the device token, and registers it with the server.
    /// Call after successful login.
    /// </summary>
    public async Task RegisterAsync()
    {
        if (!_tokenProvider.IsSupported)
        {
            Console.WriteLine("[PushRegistration] Push not supported on this platform");
            return;
        }

        try
        {
            var token = await _tokenProvider.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("[PushRegistration] No token obtained (permission denied or error)");
                return;
            }

            var result = await _apiClient.RegisterDeviceTokenAsync(token, _tokenProvider.PlatformId);
            if (result.Success && result.Data != null)
            {
                await SecureStorage.Default.SetAsync(StorageKeyTokenId, result.Data.Id.ToString());
                Console.WriteLine($"[PushRegistration] Registered device token (id={result.Data.Id})");
            }
            else
            {
                Console.WriteLine($"[PushRegistration] Registration failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PushRegistration] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Unregisters the device token from the server and clears stored ID.
    /// Call before sign-out.
    /// </summary>
    public async Task UnregisterAsync()
    {
        try
        {
            var storedId = await SecureStorage.Default.GetAsync(StorageKeyTokenId);
            if (!string.IsNullOrEmpty(storedId) && Guid.TryParse(storedId, out var tokenId))
            {
                var result = await _apiClient.UnregisterDeviceTokenAsync(tokenId);
                if (result.Success)
                {
                    Console.WriteLine($"[PushRegistration] Unregistered device token (id={tokenId})");
                }
                else
                {
                    Console.WriteLine($"[PushRegistration] Unregister failed: {result.ErrorMessage}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PushRegistration] Unregister error: {ex.Message}");
        }
        finally
        {
            SecureStorage.Default.Remove(StorageKeyTokenId);
        }
    }

    private async void OnTokenRefreshed(object? sender, string newToken)
    {
        try
        {
            Console.WriteLine("[PushRegistration] Token refreshed, re-registering...");
            var result = await _apiClient.RegisterDeviceTokenAsync(newToken, _tokenProvider.PlatformId);
            if (result.Success && result.Data != null)
            {
                await SecureStorage.Default.SetAsync(StorageKeyTokenId, result.Data.Id.ToString());
                Console.WriteLine($"[PushRegistration] Re-registered with new token (id={result.Data.Id})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PushRegistration] Re-registration error: {ex.Message}");
        }
    }
}
