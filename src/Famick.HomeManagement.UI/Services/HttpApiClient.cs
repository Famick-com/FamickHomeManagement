using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.Setup;
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
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _isRefreshing;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public HttpApiClient(
        HttpClient httpClient,
        ITokenStorage tokenStorage,
        ILogger<HttpApiClient> logger)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _logger = logger;
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

    public async Task<ApiResult> LogoutAsync()
    {
        try
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await SetAuthorizationHeader();
                var request = new RefreshTokenRequest { RefreshToken = refreshToken };
                await _httpClient.PostAsJsonAsync("api/auth/logout", request, JsonOptions);
            }

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
            await SetAuthorizationHeader();
            var response = await _httpClient.PostAsync("api/auth/logout-all", null);
            await _tokenStorage.ClearTokensAsync();

            if (response.IsSuccessStatusCode)
            {
                return ApiResult.Success();
            }

            var error = await ReadErrorMessage(response);
            return ApiResult.Failure(error, (int)response.StatusCode);
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

    public async Task<ApiResult> PutAsync(string endpoint)
    {
        return await ExecuteWithRetry(async () =>
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PutAsync(endpoint, null);
            return await HandleResponse(response);
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
        var result = await action();
        Console.WriteLine($"[HttpApiClient] Initial request result: Status={result.StatusCode}, Success={result.IsSuccess}");

        if (result.StatusCode == (int)HttpStatusCode.Unauthorized && !_isRefreshing)
        {
            Console.WriteLine("[HttpApiClient] Got 401, attempting token refresh...");
            var refreshed = await TryRefreshToken();
            Console.WriteLine($"[HttpApiClient] Token refresh result: {refreshed}");
            if (refreshed)
            {
                Console.WriteLine("[HttpApiClient] Retrying request after refresh...");
                result = await action();
                Console.WriteLine($"[HttpApiClient] Retry result: Status={result.StatusCode}, Success={result.IsSuccess}");
            }
        }

        return result;
    }

    private async Task<ApiResult> ExecuteWithRetry(Func<Task<ApiResult>> action)
    {
        var result = await action();

        if (result.StatusCode == (int)HttpStatusCode.Unauthorized && !_isRefreshing)
        {
            var refreshed = await TryRefreshToken();
            if (refreshed)
            {
                result = await action();
            }
        }

        return result;
    }

    private async Task<bool> TryRefreshToken()
    {
        await _refreshLock.WaitAsync();
        try
        {
            if (_isRefreshing) return false;
            _isRefreshing = true;

            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
            {
                return false;
            }

            var result = await RefreshTokenAsync(refreshToken);
            return result.IsSuccess;
        }
        finally
        {
            _isRefreshing = false;
            _refreshLock.Release();
        }
    }

    private async Task<ApiResult<T>> HandleResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            return data != null
                ? ApiResult<T>.Success(data)
                : ApiResult<T>.Failure("Empty response", (int)response.StatusCode);
        }

        var error = await ReadErrorMessage(response);
        return ApiResult<T>.Failure(error, (int)response.StatusCode);
    }

    private async Task<ApiResult> HandleResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return ApiResult.Success();
        }

        var error = await ReadErrorMessage(response);
        return ApiResult.Failure(error, (int)response.StatusCode);
    }

    private static async Task<string> ReadErrorMessage(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content))
            {
                return response.ReasonPhrase ?? "An error occurred";
            }

            var errorDoc = JsonDocument.Parse(content);
            if (errorDoc.RootElement.TryGetProperty("error_message", out var errorMessage))
            {
                return errorMessage.GetString() ?? "An error occurred";
            }

            return content;
        }
        catch
        {
            return response.ReasonPhrase ?? "An error occurred";
        }
    }
}
