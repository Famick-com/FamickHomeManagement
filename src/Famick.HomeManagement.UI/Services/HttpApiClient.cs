using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.Setup;
using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Core.Messaging.Messages;
using Famick.HomeManagement.UI; // NavigationManagerExtensions.NavTo
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// HTTP-based implementation of IApiClient.
/// Handles authentication, token refresh, and API communication.
/// </summary>
public class HttpApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly ILogger<HttpApiClient> _logger;
    private readonly NavigationManager _navigationManager;
    private readonly IMessageBus _messageBus;
    private readonly IStepUpReauthCoordinator? _stepUpCoordinator;
    private readonly SemaphoreSlim _stepUpLock = new(1, 1);
    private Task<bool>? _refreshInFlight;
    private bool _isInStepUp;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public HttpApiClient(
        HttpClient httpClient,
        ITokenStorage tokenStorage,
        ILogger<HttpApiClient> logger,
        NavigationManager navigationManager,
        IMessageBus messageBus,
        IStepUpReauthCoordinator? stepUpCoordinator = null)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _logger = logger;
        _navigationManager = navigationManager;
        _messageBus = messageBus;
        _stepUpCoordinator = stepUpCoordinator;
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
                if (loginResponse != null)
                {
                    await _tokenStorage.SetTokensAsync(loginResponse.AccessToken, loginResponse.RefreshToken);
                    return ApiResult<LoginResponse>.Success(loginResponse);
                }
            }

            var error = await ReadErrorMessage(response);
            return ApiResult<LoginResponse>.Failure(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return ApiResult<LoginResponse>.Failure("Login failed. Please try again.");
        }
    }

    public async Task<ApiResult<LoginResponse>> TryHaIngressSsoAsync()
    {
        try
        {
            // No body and no Authorization header — under HA Ingress the server
            // authenticates from the Supervisor-injected X-Remote-User-* headers.
            // 404 => not an Ingress deployment; 401 => not an Ingress-authenticated
            // request. Both are normal "no SSO, show login" outcomes.
            var response = await _httpClient.PostAsync("api/auth/ha-ingress", content: null);

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
                if (loginResponse != null)
                {
                    await _tokenStorage.SetTokensAsync(loginResponse.AccessToken, loginResponse.RefreshToken);
                    return ApiResult<LoginResponse>.Success(loginResponse);
                }
            }

            return ApiResult<LoginResponse>.Failure("HA Ingress SSO unavailable", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            // Network/parse failure must never block the login fallback.
            _logger.LogDebug(ex, "HA Ingress SSO attempt failed");
            return ApiResult<LoginResponse>.Failure("HA Ingress SSO failed");
        }
    }

    public async Task<ApiResult<RefreshTokenResponse>> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var request = new RefreshTokenRequest { RefreshToken = refreshToken };
            var response = await _httpClient.PostAsJsonAsync("api/auth/refresh", request, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(JsonOptions);
                if (refreshResponse != null)
                {
                    await _tokenStorage.SetTokensAsync(refreshResponse.AccessToken, refreshResponse.RefreshToken);
                    return ApiResult<RefreshTokenResponse>.Success(refreshResponse);
                }
            }

            var error = await ReadErrorMessage(response);
            return ApiResult<RefreshTokenResponse>.Failure(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            return ApiResult<RefreshTokenResponse>.Failure("Token refresh failed.");
        }
    }

    public async Task<ApiResult<ReauthResponse>> ReauthAsync(string password)
    {
        try
        {
            await SetAuthorizationHeader();
            var request = new ReauthRequest { Password = password };
            var response = await _httpClient.PostAsJsonAsync("api/auth/reauth", request, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                var reauthResponse = await response.Content.ReadFromJsonAsync<ReauthResponse>(JsonOptions);
                if (reauthResponse != null)
                {
                    // Only swap the access token — the existing refresh token + family
                    // is preserved (server contract on /api/auth/reauth).
                    await _tokenStorage.SetAccessTokenAsync(reauthResponse.AccessToken);
                    return ApiResult<ReauthResponse>.Success(reauthResponse);
                }
            }

            var error = await ReadErrorMessage(response);
            return ApiResult<ReauthResponse>.Failure(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reauth failed");
            return ApiResult<ReauthResponse>.Failure("Reauth failed. Please try again.");
        }
    }

    public async Task<ApiResult> LogoutAsync()
    {
        try
        {
            // Always ask the server: the refresh token is in a cookie this app cannot see, and only
            // the server can revoke it and clear the cookie. A leftover legacy token goes in the body.
            await SetAuthorizationHeader();
            var request = new RefreshTokenRequest { RefreshToken = await _tokenStorage.GetRefreshTokenAsync() ?? string.Empty };
            await _httpClient.PostAsJsonAsync("api/auth/logout", request, JsonOptions);

            await _tokenStorage.ClearTokensAsync();
            return ApiResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed");
            await _tokenStorage.ClearTokensAsync();
            return ApiResult.Success(); // Still consider it a success
        }
    }

    public async Task<ApiResult> LogoutAllAsync()
    {
        try
        {
            // Logout-all needs to know who is asking, so an expired access token is refreshed and
            // the call retried, the same as any other authenticated request.
            var result = await ExecuteWithRetry(async () =>
            {
                await SetAuthorizationHeader();
                return await HandleResponse(await _httpClient.PostAsync("api/auth/logout-all", null));
            });
            await _tokenStorage.ClearTokensAsync();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout all failed");
            await _tokenStorage.ClearTokensAsync();
            return ApiResult.Failure("Logout failed.");
        }
    }

    public async Task<ApiResult<RegisterResponse>> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                var registerResponse = await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions);
                if (registerResponse != null)
                {
                    // Store tokens if auto-login was enabled
                    if (!string.IsNullOrEmpty(registerResponse.AccessToken) &&
                        !string.IsNullOrEmpty(registerResponse.RefreshToken))
                    {
                        await _tokenStorage.SetTokensAsync(registerResponse.AccessToken, registerResponse.RefreshToken);
                    }
                    return ApiResult<RegisterResponse>.Success(registerResponse);
                }
            }

            var error = await ReadErrorMessage(response);
            return ApiResult<RegisterResponse>.Failure(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed");
            return ApiResult<RegisterResponse>.Failure("Registration failed. Please try again.");
        }
    }

    public async Task<ApiResult<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password", request, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                var forgotResponse = await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>(JsonOptions);
                if (forgotResponse != null)
                {
                    return ApiResult<ForgotPasswordResponse>.Success(forgotResponse);
                }
            }

            var error = await ReadErrorMessage(response);
            return ApiResult<ForgotPasswordResponse>.Failure(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forgot password request failed");
            return ApiResult<ForgotPasswordResponse>.Failure("Request failed. Please try again.");
        }
    }

    public async Task<ApiResult<ValidateResetTokenResponse>> ValidateResetTokenAsync(string token)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/auth/validate-reset-token?token={Uri.EscapeDataString(token)}");

            if (response.IsSuccessStatusCode)
            {
                var validateResponse = await response.Content.ReadFromJsonAsync<ValidateResetTokenResponse>(JsonOptions);
                if (validateResponse != null)
                {
                    return ApiResult<ValidateResetTokenResponse>.Success(validateResponse);
                }
            }

            var error = await ReadErrorMessage(response);
            return ApiResult<ValidateResetTokenResponse>.Failure(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            return ApiResult<ValidateResetTokenResponse>.Failure("Validation failed. Please try again.");
        }
    }

    public async Task<ApiResult<ResetPasswordResponse>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/reset-password", request, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                var resetResponse = await response.Content.ReadFromJsonAsync<ResetPasswordResponse>(JsonOptions);
                if (resetResponse != null)
                {
                    return ApiResult<ResetPasswordResponse>.Success(resetResponse);
                }
            }

            var error = await ReadErrorMessage(response);
            return ApiResult<ResetPasswordResponse>.Failure(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password reset failed");
            return ApiResult<ResetPasswordResponse>.Failure("Password reset failed. Please try again.");
        }
    }

    public async Task<ApiResult<SetupStatusResponse>> GetSetupStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/setup/status");

            if (response.IsSuccessStatusCode)
            {
                var statusResponse = await response.Content.ReadFromJsonAsync<SetupStatusResponse>(JsonOptions);
                if (statusResponse != null)
                {
                    return ApiResult<SetupStatusResponse>.Success(statusResponse);
                }
            }

            var error = await ReadErrorMessage(response);
            return ApiResult<SetupStatusResponse>.Failure(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get setup status");
            return ApiResult<SetupStatusResponse>.Failure("Failed to check setup status.");
        }
    }

    public async Task<ApiResult<T>> GetAsync<T>(string endpoint)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.GetAsync(endpoint);
            return await HandleResponse<T>(response);
        });
    }

    public async Task<ApiResult> GetAsync(string endpoint)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.GetAsync(endpoint);
            return await HandleResponse(response);
        });
    }

    public async Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, JsonOptions);
            return await HandleResponse<TResponse>(response);
        });
    }

    public async Task<ApiResult> PostAsync<TRequest>(string endpoint, TRequest request)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, JsonOptions);
            return await HandleResponse(response);
        });
    }

    public async Task<ApiResult> PostAsync(string endpoint)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PostAsync(endpoint, null);
            return await HandleResponse(response);
        });
    }

    public async Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync(endpoint, request, JsonOptions);
            return await HandleResponse<TResponse>(response);
        });
    }

    public async Task<ApiResult> PutAsync<TRequest>(string endpoint, TRequest request)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync(endpoint, request, JsonOptions);
            return await HandleResponse(response);
        });
    }

    public async Task<ApiResult> DeleteAsync(string endpoint)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync(endpoint);
            return await HandleResponse(response);
        });
    }

    public async Task<ApiResult<TResponse>> DeleteAsync<TResponse>(string endpoint)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync(endpoint);
            return await HandleResponse<TResponse>(response);
        });
    }

    public async Task<ApiResult> PutAsync(string endpoint)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PutAsync(endpoint, null);
            return await HandleResponse(response);
        });
    }

    public async Task<ApiResult<byte[]>> PostForFileAsync<TRequest>(string endpoint, TRequest request)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, JsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return ApiResult<byte[]>.Success(await response.Content.ReadAsByteArrayAsync());
            }

            return ApiResult<byte[]>.Failure(await ReadErrorMessage(response), (int)response.StatusCode);
        });
    }

    public async Task<ApiResult<TResponse>> PostMultipartAsync<TResponse>(string endpoint, MultipartFormDataContent content)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PostAsync(endpoint, content);
            return await HandleResponse<TResponse>(response);
        });
    }

    private async Task SetAuthorizationHeader()
    {
        // Always clear existing header first to avoid stale token issues
        _httpClient.DefaultRequestHeaders.Authorization = null;

        var token = await _tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Console.WriteLine($"[HttpApiClient] Set auth header with token: {token.Substring(0, Math.Min(20, token.Length))}...");
        }
        else
        {
            Console.WriteLine("[HttpApiClient] No token available - cleared auth header");
        }
    }

    private async Task<ApiResult<T>> ExecuteWithRetry<T>(Func<Task<ApiResult<T>>> action)
    {
        var tokenUsed = await _tokenStorage.GetAccessTokenAsync();
        var result = await action();
        Console.WriteLine($"[HttpApiClient] Initial request result: Status={result.StatusCode}, Success={result.IsSuccess}");

        if (result.StatusCode == (int)HttpStatusCode.Unauthorized && await RecoverFromUnauthorizedAsync(tokenUsed))
        {
            Console.WriteLine("[HttpApiClient] Retrying request after refresh...");
            result = await action();
            Console.WriteLine($"[HttpApiClient] Retry result: Status={result.StatusCode}, Success={result.IsSuccess}");
        }

        if (await TryStepUpAsync(result))
        {
            Console.WriteLine("[HttpApiClient] Retrying request after step-up reauth...");
            result = await action();
        }

        InterceptForbiddenErrorCodes(result);

        return result;
    }

    private async Task<ApiResult> ExecuteWithRetry(Func<Task<ApiResult>> action)
    {
        var tokenUsed = await _tokenStorage.GetAccessTokenAsync();
        var result = await action();

        if (result.StatusCode == (int)HttpStatusCode.Unauthorized && await RecoverFromUnauthorizedAsync(tokenUsed))
        {
            result = await action();
        }

        if (await TryStepUpAsync(result))
        {
            result = await action();
        }

        InterceptForbiddenErrorCodes(result);

        return result;
    }

    /// <summary>
    /// Phase 2.5 — if <paramref name="result"/> is a 403 STEP_UP_REQUIRED and
    /// a coordinator is registered, ask the user to re-authenticate. Returns
    /// true if reauth succeeded (caller should retry the original action),
    /// false otherwise (caller should let the 403 surface).
    /// </summary>
    private async Task<bool> TryStepUpAsync(ApiResult result)
    {
        if (_stepUpCoordinator == null
            || _isInStepUp
            || result.StatusCode != (int)HttpStatusCode.Forbidden
            || result.ErrorCode != "STEP_UP_REQUIRED")
        {
            return false;
        }

        await _stepUpLock.WaitAsync();
        try
        {
            if (_isInStepUp) return false;
            _isInStepUp = true;
            return await _stepUpCoordinator.RequestStepUpAsync();
        }
        finally
        {
            _isInStepUp = false;
            _stepUpLock.Release();
        }
    }

    /// <summary>
    /// After a 401: true when the request is worth retrying. If another request already replaced
    /// the access token this one was sent with, retry with the new one; otherwise refresh.
    /// </summary>
    private async Task<bool> RecoverFromUnauthorizedAsync(string? tokenUsed)
    {
        var current = await _tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(current) && current != tokenUsed)
        {
            return true;
        }

        if (await RefreshSessionAsync())
        {
            return true;
        }

        _messageBus.Publish(new SessionExpiredMessage("Session expired or revoked") { Source = "blazor" });
        return false;
    }

    /// <inheritdoc />
    public Task<bool> RefreshSessionAsync()
    {
        // One refresh at a time, shared by everyone who asks while it runs. Several requests
        // failing together must not each rotate the token: each rotation revokes the token the
        // others are about to present.
        return _refreshInFlight ??= RunRefreshAsync();

        async Task<bool> RunRefreshAsync()
        {
            // Yield first so this never completes synchronously: the field above must hold the
            // task before the finally below clears it.
            await Task.Yield();
            try
            {
                // Empty means "use the cookie". A token left in localStorage by an older build is
                // sent instead, once, and the response moves that session into the cookie.
                var legacyToken = await _tokenStorage.GetRefreshTokenAsync();
                var result = await RefreshTokenAsync(legacyToken ?? string.Empty);
                if (!result.IsSuccess)
                {
                    await _tokenStorage.ClearTokensAsync();
                }
                return result.IsSuccess;
            }
            finally
            {
                _refreshInFlight = null;
            }
        }
    }

    private void InterceptForbiddenErrorCodes(ApiResult result)
    {
        if (result.StatusCode != (int)HttpStatusCode.Forbidden || string.IsNullOrEmpty(result.ErrorCode))
            return;

        if (result.ErrorCode == "MUST_CHANGE_PASSWORD")
        {
            _messageBus.Publish(new MustChangePasswordMessage("Server requires password change") { Source = "blazor" });
            _navigationManager.NavTo("/force-change-password");
        }
        else if (result.ErrorCode == "MUST_ACCEPT_TERMS")
        {
            _messageBus.Publish(new MustAcceptTermsMessage("Server requires terms acceptance") { Source = "blazor" });
            _navigationManager.NavTo("/accept-terms");
        }
        // STEP_UP_REQUIRED is handled earlier in ExecuteWithRetry by TryStepUpAsync.
        // If it gets here, the user cancelled the reauth modal — let the 403
        // surface to the caller (the originating page handles its own error UX).
    }

    private async Task<ApiResult<T>> HandleResponse<T>(HttpResponseMessage response)
    {
        // A 204 has no body at all, so ReadFromJsonAsync below would throw. Endpoints that answer
        // 204 to mean "the thing is gone" need that to read as success-with-no-data, not an error.
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return ApiResult<T>.NoContent();
        }

        if (response.IsSuccessStatusCode)
        {
            T? data;
            try
            {
                data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            }
            catch (JsonException ex)
            {
                // One unmappable property fails the whole payload, and callers that treat a
                // failed result as "nothing to show" then render an empty screen that is
                // indistinguishable from a legitimately empty one. Say so out loud: this was
                // invisible for the setup wizard until somebody read the API response by hand.
                _logger.LogError(ex,
                    "Could not read a {Type} from {Endpoint}; the response did not map onto it",
                    typeof(T).Name, response.RequestMessage?.RequestUri);
                throw;
            }

            return data != null
                ? ApiResult<T>.Success(data)
                : ApiResult<T>.Failure("Empty response", (int)response.StatusCode);
        }

        var (error, code, body) = await ReadErrorDetails(response);
        var failure = !string.IsNullOrEmpty(code)
            ? ApiResult<T>.FailureWithCode(error, code, (int)response.StatusCode)
            : ApiResult<T>.Failure(error, (int)response.StatusCode);
        failure.ErrorBody = body;
        return failure;
    }

    private async Task<ApiResult> HandleResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return ApiResult.Success();
        }

        var (error, code, body) = await ReadErrorDetails(response);
        var failure = !string.IsNullOrEmpty(code)
            ? ApiResult.FailureWithCode(error, code, (int)response.StatusCode)
            : ApiResult.Failure(error, (int)response.StatusCode);
        failure.ErrorBody = body;
        return failure;
    }

    private static async Task<string> ReadErrorMessage(HttpResponseMessage response)
    {
        var (message, _, _) = await ReadErrorDetails(response);
        return message;
    }

    private static async Task<(string Message, string? Code, JsonElement? Body)> ReadErrorDetails(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content))
            {
                return (response.ReasonPhrase ?? "An error occurred", null, null);
            }

            using var errorDoc = JsonDocument.Parse(content);
            var body = errorDoc.RootElement.Clone();
            string? code = null;
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("code", out var codeProp))
            {
                code = codeProp.GetString();
            }

            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("error_message", out var errorMessage))
            {
                return (errorMessage.GetString() ?? "An error occurred", code, body);
            }

            return (content, code, body);
        }
        catch
        {
            return (response.ReasonPhrase ?? "An error occurred", null, null);
        }
    }
}
