using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Service for handling store integration OAuth flows using WebAuthenticator.
/// </summary>
public class StoreIntegrationOAuthService
{
    private readonly ShoppingApiClient _apiClient;
    private readonly ApiSettings _apiSettings;

    public const string CallbackScheme = "com.famick.homemanagement";
    public const string CallbackHost = "store-oauth";
    public const string CallbackPath = "/callback";
    public static readonly string CallbackUrl = $"{CallbackScheme}://{CallbackHost}{CallbackPath}";

    public StoreIntegrationOAuthService(ShoppingApiClient apiClient, ApiSettings apiSettings)
    {
        _apiClient = apiClient;
        _apiSettings = apiSettings;
    }

    /// <summary>
    /// Initiates the OAuth flow for a store integration plugin.
    /// Opens the browser, handles the redirect, and completes the token exchange.
    /// </summary>
    public async Task<StoreOAuthResult> ConnectStoreAsync(string pluginId, Guid shoppingLocationId)
    {
        try
        {
            // Build the server-side callback URL that the OAuth provider will redirect to
            var serverCallbackUrl = $"{_apiSettings.BaseUrl.TrimEnd('/')}/api/v1/storeintegrations/oauth/{Uri.EscapeDataString(pluginId)}/mobile-callback";

            // Get the OAuth authorization URL from the server
            var authResult = await _apiClient.GetStoreOAuthUrlAsync(pluginId, shoppingLocationId, serverCallbackUrl);
            if (!authResult.Success || authResult.Data == null)
            {
                return StoreOAuthResult.Failed(authResult.ErrorMessage ?? "Failed to get authorization URL");
            }

            var authorizationUrl = authResult.Data.AuthorizationUrl;

            // Open browser for OAuth authorization
            WebAuthenticatorResult webResult;
            try
            {
                webResult = await WebAuthenticator.Default.AuthenticateAsync(
                    new Uri(authorizationUrl),
                    new Uri(CallbackUrl));
            }
            catch (TaskCanceledException)
            {
                return StoreOAuthResult.Cancelled();
            }
            catch (OperationCanceledException)
            {
                return StoreOAuthResult.Cancelled();
            }

            // Check for error in callback
            if (webResult.Properties.TryGetValue("error", out var error) && !string.IsNullOrEmpty(error))
            {
                webResult.Properties.TryGetValue("error_description", out var errorDesc);
                return StoreOAuthResult.Failed(errorDesc ?? error);
            }

            // Extract code and state from the callback
            var code = webResult.Properties.TryGetValue("code", out var codeValue) ? codeValue : null;
            var state = webResult.Properties.TryGetValue("state", out var stateValue) ? stateValue : null;

            if (string.IsNullOrEmpty(code))
            {
                return StoreOAuthResult.Failed("No authorization code received");
            }

            // Complete the OAuth flow by exchanging the code for tokens
            var callbackResult = await _apiClient.CompleteStoreOAuthAsync(
                pluginId,
                new StoreOAuthCallbackRequest { Code = code, State = state ?? string.Empty },
                serverCallbackUrl);

            if (!callbackResult.Success || callbackResult.Data == null)
            {
                return StoreOAuthResult.Failed(callbackResult.ErrorMessage ?? "Failed to complete authorization");
            }

            return callbackResult.Data.Success
                ? StoreOAuthResult.Succeeded()
                : StoreOAuthResult.Failed("Authorization was not successful");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Store OAuth error: {ex}");
            return StoreOAuthResult.Failed($"Connection error: {ex.Message}");
        }
    }
}
