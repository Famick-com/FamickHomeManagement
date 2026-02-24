using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Authentication;

namespace Famick.HomeManagement.Web.Services;

/// <summary>
/// HTTP client wrapper for communicating with the Famick cloud API (app.famick.com).
/// Handles authentication, token refresh, and typed request/response methods.
/// </summary>
public class CloudApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudApiClient> _logger;
    private string? _accessToken;
    private string? _refreshToken;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _isRefreshing;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public CloudApiClient(HttpClient httpClient, ILogger<CloudApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current refresh token for session persistence.
    /// </summary>
    public string? RefreshToken => _refreshToken;

    /// <summary>
    /// Authenticate against the cloud API with email and password.
    /// </summary>
    public async Task<CloudApiResult<LoginResponse>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var request = new LoginRequest { Email = email, Password = password, RememberMe = true };
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, JsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct);
                if (loginResponse != null)
                {
                    _accessToken = loginResponse.AccessToken;
                    _refreshToken = loginResponse.RefreshToken;
                    return CloudApiResult<LoginResponse>.Ok(loginResponse);
                }
            }
            var error = await ReadErrorMessage(response, ct);
            return CloudApiResult<LoginResponse>.Fail(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud login failed for {Email}", email);
            return CloudApiResult<LoginResponse>.Fail($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Register a new account on the cloud API.
    /// </summary>
    public async Task<CloudApiResult<RegisterResponse>> RegisterAsync(
        string email, string password, string firstName, string lastName, CancellationToken ct = default)
    {
        var request = new RegisterRequest
        {
            Email = email,
            Password = password,
            ConfirmPassword = password,
            FirstName = firstName,
            LastName = lastName
        };
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, JsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                var registerResponse = await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions, ct);
                if (registerResponse != null)
                {
                    _accessToken = registerResponse.AccessToken;
                    _refreshToken = registerResponse.RefreshToken;
                    return CloudApiResult<RegisterResponse>.Ok(registerResponse);
                }
            }
            var error = await ReadErrorMessage(response, ct);
            return CloudApiResult<RegisterResponse>.Fail(error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud registration failed for {Email}", email);
            return CloudApiResult<RegisterResponse>.Fail($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Restore authentication from a stored refresh token (for resume).
    /// </summary>
    public async Task<bool> RestoreAuthAsync(string refreshToken, CancellationToken ct = default)
    {
        _refreshToken = refreshToken;
        return await TryRefreshTokenAsync(ct);
    }

    /// <summary>
    /// GET request with automatic token refresh on 401.
    /// </summary>
    public async Task<CloudApiResult<T>> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        return await ExecuteWithRetry(async () =>
        {
            SetAuthHeader();
            var response = await _httpClient.GetAsync(endpoint, ct);
            return await HandleResponse<T>(response, ct);
        }, ct);
    }

    /// <summary>
    /// POST request with automatic token refresh on 401.
    /// </summary>
    public async Task<CloudApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string endpoint, TRequest body, CancellationToken ct = default)
    {
        return await ExecuteWithRetry(async () =>
        {
            SetAuthHeader();
            var response = await _httpClient.PostAsJsonAsync(endpoint, body, JsonOptions, ct);
            return await HandleResponse<TResponse>(response, ct);
        }, ct);
    }

    /// <summary>
    /// POST request without response body.
    /// </summary>
    public async Task<CloudApiResult> PostAsync<TRequest>(
        string endpoint, TRequest body, CancellationToken ct = default)
    {
        return await ExecuteWithRetry(async () =>
        {
            SetAuthHeader();
            var response = await _httpClient.PostAsJsonAsync(endpoint, body, JsonOptions, ct);
            return await HandleVoidResponse(response, ct);
        }, ct);
    }

    /// <summary>
    /// PUT request with automatic token refresh on 401.
    /// </summary>
    public async Task<CloudApiResult<TResponse>> PutAsync<TRequest, TResponse>(
        string endpoint, TRequest body, CancellationToken ct = default)
    {
        return await ExecuteWithRetry(async () =>
        {
            SetAuthHeader();
            var response = await _httpClient.PutAsJsonAsync(endpoint, body, JsonOptions, ct);
            return await HandleResponse<TResponse>(response, ct);
        }, ct);
    }

    /// <summary>
    /// POST multipart/form-data for file uploads.
    /// </summary>
    public async Task<CloudApiResult<TResponse>> PostFileAsync<TResponse>(
        string endpoint, Stream fileStream, string fileName, CancellationToken ct = default)
    {
        return await ExecuteWithRetry(async () =>
        {
            SetAuthHeader();
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", fileName);
            var response = await _httpClient.PostAsync(endpoint, content, ct);
            return await HandleResponse<TResponse>(response, ct);
        }, ct);
    }

    private void SetAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            !string.IsNullOrEmpty(_accessToken)
                ? new AuthenticationHeaderValue("Bearer", _accessToken)
                : null;
    }

    private async Task<CloudApiResult<T>> ExecuteWithRetry<T>(
        Func<Task<CloudApiResult<T>>> action, CancellationToken ct)
    {
        var result = await action();
        if (result.StatusCode == (int)HttpStatusCode.Unauthorized && !_isRefreshing)
        {
            if (await TryRefreshTokenAsync(ct))
            {
                result = await action();
            }
        }
        return result;
    }

    private async Task<CloudApiResult> ExecuteWithRetry(
        Func<Task<CloudApiResult>> action, CancellationToken ct)
    {
        var result = await action();
        if (result.StatusCode == (int)HttpStatusCode.Unauthorized && !_isRefreshing)
        {
            if (await TryRefreshTokenAsync(ct))
            {
                result = await action();
            }
        }
        return result;
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            if (_isRefreshing) return false;
            _isRefreshing = true;

            if (string.IsNullOrEmpty(_refreshToken)) return false;

            var request = new RefreshTokenRequest { RefreshToken = _refreshToken };
            var response = await _httpClient.PostAsJsonAsync("api/auth/refresh", request, JsonOptions, ct);

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct);
                if (tokenResponse != null)
                {
                    _accessToken = tokenResponse.AccessToken;
                    _refreshToken = tokenResponse.RefreshToken;
                    return true;
                }
            }

            _logger.LogWarning("Cloud token refresh failed with status {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud token refresh failed");
            return false;
        }
        finally
        {
            _isRefreshing = false;
            _refreshLock.Release();
        }
    }

    private static async Task<CloudApiResult<T>> HandleResponse<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            return data != null
                ? CloudApiResult<T>.Ok(data)
                : CloudApiResult<T>.Fail("Empty response", (int)response.StatusCode);
        }
        var error = await ReadErrorMessage(response, ct);
        return CloudApiResult<T>.Fail(error, (int)response.StatusCode);
    }

    private static async Task<CloudApiResult> HandleVoidResponse(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return CloudApiResult.Ok();

        var error = await ReadErrorMessage(response, ct);
        return CloudApiResult.Fail(error, (int)response.StatusCode);
    }

    private static async Task<string> ReadErrorMessage(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrEmpty(content))
                return response.ReasonPhrase ?? "An error occurred";

            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("error_message", out var msg))
                return msg.GetString() ?? "An error occurred";

            return content;
        }
        catch
        {
            return response.ReasonPhrase ?? "An error occurred";
        }
    }
}

/// <summary>
/// Result of a cloud API call with typed data.
/// </summary>
public class CloudApiResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Data { get; private init; }
    public string? ErrorMessage { get; private init; }
    public int StatusCode { get; private init; }

    public static CloudApiResult<T> Ok(T data) => new() { IsSuccess = true, Data = data, StatusCode = 200 };
    public static CloudApiResult<T> Fail(string error, int statusCode = 0) =>
        new() { IsSuccess = false, ErrorMessage = error, StatusCode = statusCode };
}

/// <summary>
/// Result of a cloud API call without data.
/// </summary>
public class CloudApiResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorMessage { get; private init; }
    public int StatusCode { get; private init; }

    public static CloudApiResult Ok() => new() { IsSuccess = true, StatusCode = 200 };
    public static CloudApiResult Fail(string error, int statusCode = 0) =>
        new() { IsSuccess = false, ErrorMessage = error, StatusCode = statusCode };
}
