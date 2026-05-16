using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Service for handling OAuth authentication flow using WebAuthenticator.
/// </summary>
public class OAuthService
{
    private readonly ShoppingApiClient _apiClient;
    private readonly TokenStorage _tokenStorage;
    private readonly TenantStorage _tenantStorage;
    private readonly ApiSettings _apiSettings;
    private readonly OnboardingService _onboardingService;
    private readonly IAppleSignInService? _appleSignInService;
    private readonly IGoogleSignInService? _googleSignInService;

    /// <summary>
    /// The callback URL scheme for OAuth redirects.
    /// Must match the URL scheme registered in iOS Info.plist and Android manifest.
    /// </summary>
    public const string CallbackScheme = "com.famick.homemanagement";
    public const string CallbackHost = "oauth";
    public const string CallbackPath = "/callback";
    public static readonly string CallbackUrl = $"{CallbackScheme}://{CallbackHost}{CallbackPath}";

    private AuthConfiguration? _cachedConfig;
    private DateTime? _configCacheTime;
    private static readonly TimeSpan ConfigCacheDuration = TimeSpan.FromMinutes(5);

    public OAuthService(
        ShoppingApiClient apiClient,
        TokenStorage tokenStorage,
        TenantStorage tenantStorage,
        ApiSettings apiSettings,
        OnboardingService onboardingService,
        IAppleSignInService? appleSignInService = null,
        IGoogleSignInService? googleSignInService = null)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _tenantStorage = tenantStorage;
        _apiSettings = apiSettings;
        _onboardingService = onboardingService;
        _appleSignInService = appleSignInService;
        _googleSignInService = googleSignInService;
    }

    /// <summary>
    /// Gets the authentication configuration from the server.
    /// Results are cached for 5 minutes to avoid repeated API calls.
    /// </summary>
    public async Task<ApiResult<AuthConfiguration>> GetAuthConfigurationAsync(bool forceRefresh = false)
    {
        // Return cached config if still valid
        if (!forceRefresh &&
            _cachedConfig != null &&
            _configCacheTime.HasValue &&
            DateTime.UtcNow - _configCacheTime.Value < ConfigCacheDuration)
        {
            return ApiResult<AuthConfiguration>.Ok(_cachedConfig);
        }

        var result = await _apiClient.GetAuthConfigurationAsync();

        if (result.Success && result.Data != null)
        {
            _cachedConfig = result.Data;
            _configCacheTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Performs the full OAuth login flow for a provider.
    /// </summary>
    /// <param name="provider">Provider name (Google, Apple, OIDC)</param>
    /// <param name="rememberMe">Whether to extend the refresh token lifetime</param>
    /// <returns>Result indicating success or failure with error message</returns>
    public async Task<OAuthLoginResult> LoginWithProviderAsync(string provider, bool rememberMe = false)
    {
        // Check if we should use native Apple Sign in
        if (provider.Equals("Apple", StringComparison.OrdinalIgnoreCase) &&
            _appleSignInService?.IsAvailable == true)
        {
            return await LoginWithNativeAppleAsync(rememberMe);
        }

        // Check if we should use native Google Sign in
        if (provider.Equals("Google", StringComparison.OrdinalIgnoreCase) &&
            _googleSignInService?.IsAvailable == true)
        {
            return await LoginWithNativeGoogleAsync(rememberMe);
        }

        // Fall back to web-based OAuth flow
        return await LoginWithWebOAuthAsync(provider, rememberMe);
    }

    /// <summary>
    /// Performs native Apple Sign in (iOS only).
    /// </summary>
    private async Task<OAuthLoginResult> LoginWithNativeAppleAsync(bool rememberMe)
    {
        try
        {
            // Step 1: Perform native Apple Sign in
            AppleSignInCredential credential;
            try
            {
                credential = await _appleSignInService!.SignInAsync();
            }
            catch (OperationCanceledException)
            {
                return OAuthLoginResult.Cancelled();
            }
            catch (AppleSignInException ex) when (ex.ErrorCode == AppleSignInErrorCode.Canceled)
            {
                return OAuthLoginResult.Cancelled();
            }
            catch (AppleSignInException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Apple Sign in error: {ex.ErrorCode} - {ex.Message}");
                return OAuthLoginResult.Failed(ex.Message);
            }

            // Step 2: Send credentials to backend
            var request = new NativeAppleSignInRequest
            {
                IdentityToken = credential.IdentityToken,
                AuthorizationCode = credential.AuthorizationCode,
                UserIdentifier = credential.UserIdentifier,
                Email = credential.Email,
                FullName = !string.IsNullOrEmpty(credential.GivenName) || !string.IsNullOrEmpty(credential.FamilyName)
                    ? new AppleUserName
                    {
                        GivenName = credential.GivenName,
                        FamilyName = credential.FamilyName
                    }
                    : null,
                RememberMe = rememberMe
            };

            var callbackResult = await _apiClient.ProcessNativeAppleSignInAsync(request);

            if (!callbackResult.Success || callbackResult.Data == null)
            {
                return OAuthLoginResult.Failed(callbackResult.ErrorMessage ?? "Authentication failed");
            }

            // Step 3: Store the tokens
            await _tokenStorage.SetTokensAsync(
                callbackResult.Data.AccessToken,
                callbackResult.Data.RefreshToken);
            // Phase 4 chunk 4.G — change-detection on the local-server URL.
            LocalServerChangeDetector.ObserveLogin(callbackResult.Data.LocalServer);

            // Step 4: Update tenant information
            var tenantName = callbackResult.Data.Tenant?.Name;
            if (!string.IsNullOrEmpty(tenantName))
            {
                _apiSettings.TenantName = tenantName;
                await _tenantStorage.SetTenantNameAsync(tenantName);
            }

            // Step 5: Mark onboarding as complete
            _onboardingService.MarkOnboardingCompleted();
            _apiSettings.MarkServerConfigured();

            return OAuthLoginResult.Succeeded(callbackResult.Data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Native Apple Sign in error: {ex}");
            return OAuthLoginResult.Failed($"Connection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs native Google Sign in (iOS and Android).
    /// </summary>
    private async Task<OAuthLoginResult> LoginWithNativeGoogleAsync(bool rememberMe)
    {
        try
        {
            // Step 1: Perform native Google Sign in
            GoogleSignInCredential credential;
            try
            {
                credential = await _googleSignInService!.SignInAsync();
            }
            catch (OperationCanceledException)
            {
                return OAuthLoginResult.Cancelled();
            }
            catch (GoogleSignInException ex) when (ex.ErrorCode == GoogleSignInErrorCode.Canceled)
            {
                return OAuthLoginResult.Cancelled();
            }
            catch (GoogleSignInException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Google Sign in error: {ex.ErrorCode} - {ex.Message}");
                return OAuthLoginResult.Failed(ex.Message);
            }

            // Step 2: Send credentials to backend
            var request = new NativeGoogleSignInRequest
            {
                IdToken = credential.IdToken,
                RememberMe = rememberMe
            };

            var callbackResult = await _apiClient.ProcessNativeGoogleSignInAsync(request);

            if (!callbackResult.Success || callbackResult.Data == null)
            {
                return OAuthLoginResult.Failed(callbackResult.ErrorMessage ?? "Authentication failed");
            }

            // Step 3: Store the tokens
            await _tokenStorage.SetTokensAsync(
                callbackResult.Data.AccessToken,
                callbackResult.Data.RefreshToken);
            // Phase 4 chunk 4.G — change-detection on the local-server URL.
            LocalServerChangeDetector.ObserveLogin(callbackResult.Data.LocalServer);

            // Step 4: Update tenant information
            var tenantName = callbackResult.Data.Tenant?.Name;
            if (!string.IsNullOrEmpty(tenantName))
            {
                _apiSettings.TenantName = tenantName;
                await _tenantStorage.SetTenantNameAsync(tenantName);
            }

            // Step 5: Mark onboarding as complete
            _onboardingService.MarkOnboardingCompleted();
            _apiSettings.MarkServerConfigured();

            return OAuthLoginResult.Succeeded(callbackResult.Data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Native Google Sign in error: {ex}");
            return OAuthLoginResult.Failed($"Connection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs web-based OAuth flow using WebAuthenticator.
    /// For providers like Google that don't support custom URL schemes,
    /// we use a server-side redirect approach.
    /// </summary>
    private async Task<OAuthLoginResult> LoginWithWebOAuthAsync(string provider, bool rememberMe)
    {
        try
        {
            // Step 1: Build the server-side callback URL
            // Google OAuth doesn't allow custom URL schemes, so we redirect through the server
            var serverCallbackUrl = $"{_apiSettings.BaseUrl.TrimEnd('/')}/api/auth/external/{provider.ToLower()}/mobile-callback";

            // Step 2: Get the authorization URL from the server
            var challengeResult = await _apiClient.GetOAuthChallengeAsync(provider, serverCallbackUrl);

            if (!challengeResult.Success || challengeResult.Data == null)
            {
                return OAuthLoginResult.Failed(challengeResult.ErrorMessage ?? "Failed to get authorization URL");
            }

            var authorizationUrl = challengeResult.Data.AuthorizationUrl;
            var expectedState = challengeResult.Data.State;

            // Step 3: Open the browser for OAuth authentication
            // WebAuthenticator will listen for the custom URL scheme redirect from the server
            WebAuthenticatorResult authResult;
            try
            {
                authResult = await WebAuthenticator.Default.AuthenticateAsync(
                    new Uri(authorizationUrl),
                    new Uri(CallbackUrl));
            }
            catch (TaskCanceledException)
            {
                return OAuthLoginResult.Cancelled();
            }
            catch (OperationCanceledException)
            {
                return OAuthLoginResult.Cancelled();
            }

            // Step 3: Extract code and state from the callback
            var code = authResult.Properties.TryGetValue("code", out var codeValue) ? codeValue : null;
            var state = authResult.Properties.TryGetValue("state", out var stateValue) ? stateValue : null;

            if (string.IsNullOrEmpty(code))
            {
                // Try getting from query parameters if not in properties
                return OAuthLoginResult.Failed("No authorization code received from provider");
            }

            // Step 4: Verify state parameter matches
            if (state != expectedState)
            {
                return OAuthLoginResult.Failed("Authentication session expired. Please try again.");
            }

            // Step 5: Exchange the code for tokens
            var callbackResult = await _apiClient.ProcessOAuthCallbackAsync(
                provider, code, state, rememberMe);

            if (!callbackResult.Success || callbackResult.Data == null)
            {
                return OAuthLoginResult.Failed(callbackResult.ErrorMessage ?? "Authentication failed");
            }

            // Step 6: Store the tokens
            await _tokenStorage.SetTokensAsync(
                callbackResult.Data.AccessToken,
                callbackResult.Data.RefreshToken);
            // Phase 4 chunk 4.G — change-detection on the local-server URL.
            LocalServerChangeDetector.ObserveLogin(callbackResult.Data.LocalServer);

            // Step 7: Update tenant information
            var tenantName = callbackResult.Data.Tenant?.Name;
            if (!string.IsNullOrEmpty(tenantName))
            {
                _apiSettings.TenantName = tenantName;
                await _tenantStorage.SetTenantNameAsync(tenantName);
            }

            // Step 8: Mark onboarding as complete
            _onboardingService.MarkOnboardingCompleted();
            _apiSettings.MarkServerConfigured();

            return OAuthLoginResult.Succeeded(callbackResult.Data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OAuth error: {ex}");
            return OAuthLoginResult.Failed($"Connection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Phase 3 chunk 3.C — resume an in-flight OAuth login from a Universal
    /// Link / App Link callback. Used when the OS-verified HTTPS callback
    /// (https://app.famick.com/mobile-callback/oauth/{provider}?code=...&amp;state=...)
    /// fires while the app is in the background — for example because the
    /// user backgrounded Safari / Chrome Custom Tab mid-flow and the OS
    /// handed the deep link straight to the app instead of through the
    /// in-process WebAuthenticator result.
    ///
    /// In the happy path (foreground browser, in-process result) the existing
    /// WebAuthenticator-based code path in <see cref="LoginWithOAuthAsync"/>
    /// handles the callback verbatim — this method is the fallback for the
    /// race-prone background path.
    ///
    /// Mirrors steps 5-8 of LoginWithOAuthAsync: exchange code for tokens,
    /// store tokens, update tenant name, mark onboarding complete.
    /// </summary>
    public async Task<OAuthLoginResult> ResumeFromUniversalLinkAsync(
        string provider,
        string? code,
        string? state,
        string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return OAuthLoginResult.Failed($"OAuth provider returned error: {error}");
        }

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return OAuthLoginResult.Failed("Incomplete OAuth callback from Universal Link");
        }

        try
        {
            // Same exchange path as LoginWithOAuthAsync step 5+. State verification
            // happens server-side on /auth/external/callback/{provider} — the
            // server compares against the cached state from /challenge.
            var callbackResult = await _apiClient.ProcessOAuthCallbackAsync(
                provider, code, state, rememberMe: false);

            if (!callbackResult.Success || callbackResult.Data == null)
            {
                return OAuthLoginResult.Failed(callbackResult.ErrorMessage ?? "Authentication failed");
            }

            await _tokenStorage.SetTokensAsync(
                callbackResult.Data.AccessToken,
                callbackResult.Data.RefreshToken);
            // Phase 4 chunk 4.G — change-detection on the local-server URL.
            LocalServerChangeDetector.ObserveLogin(callbackResult.Data.LocalServer);

            var tenantName = callbackResult.Data.Tenant?.Name;
            if (!string.IsNullOrEmpty(tenantName))
            {
                _apiSettings.TenantName = tenantName;
                await _tenantStorage.SetTenantNameAsync(tenantName);
            }

            _onboardingService.MarkOnboardingCompleted();
            _apiSettings.MarkServerConfigured();

            return OAuthLoginResult.Succeeded(callbackResult.Data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OAuth UL resume error: {ex}");
            return OAuthLoginResult.Failed($"Connection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the cached authentication configuration.
    /// </summary>
    public void ClearCache()
    {
        _cachedConfig = null;
        _configCacheTime = null;
    }
}

/// <summary>
/// Result of an OAuth login attempt.
/// </summary>
public class OAuthLoginResult
{
    public bool Success { get; private set; }
    public bool WasCancelled { get; private set; }
    public string? ErrorMessage { get; private set; }
    public LoginResponse? LoginResponse { get; private set; }
    public bool MustChangePassword => LoginResponse?.MustChangePassword ?? false;

    private OAuthLoginResult() { }

    public static OAuthLoginResult Succeeded(LoginResponse response) =>
        new() { Success = true, LoginResponse = response };

    public static OAuthLoginResult Cancelled() =>
        new() { Success = false, WasCancelled = true, ErrorMessage = "Authentication was cancelled" };

    public static OAuthLoginResult Failed(string message) =>
        new() { Success = false, ErrorMessage = message };
}
