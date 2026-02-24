using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.Setup;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// HTTP API client abstraction for communicating with the backend.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Login with email and password.
    /// </summary>
    Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request);

    /// <summary>
    /// Refresh the access token using a refresh token.
    /// </summary>
    Task<ApiResult<RefreshTokenResponse>> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Logout and revoke the current refresh token.
    /// </summary>
    Task<ApiResult> LogoutAsync();

    /// <summary>
    /// Logout from all devices.
    /// </summary>
    Task<ApiResult> LogoutAllAsync();

    /// <summary>
    /// Register a new user account.
    /// </summary>
    Task<ApiResult<RegisterResponse>> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Request a password reset email.
    /// </summary>
    Task<ApiResult<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request);

    /// <summary>
    /// Validate a password reset token.
    /// </summary>
    Task<ApiResult<ValidateResetTokenResponse>> ValidateResetTokenAsync(string token);

    /// <summary>
    /// Reset password using a valid token.
    /// </summary>
    Task<ApiResult<ResetPasswordResponse>> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Check if initial setup is required.
    /// </summary>
    Task<ApiResult<SetupStatusResponse>> GetSetupStatusAsync();

    /// <summary>
    /// Send a GET request to the specified endpoint.
    /// </summary>
    Task<ApiResult<T>> GetAsync<T>(string endpoint);

    /// <summary>
    /// Send a POST request with a body to the specified endpoint.
    /// </summary>
    Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest request);

    /// <summary>
    /// Send a POST request without expecting a response body.
    /// </summary>
    Task<ApiResult> PostAsync<TRequest>(string endpoint, TRequest request);

    /// <summary>
    /// Send a PUT request with a body to the specified endpoint.
    /// </summary>
    Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest request);

    /// <summary>
    /// Send a PUT request without expecting a response body.
    /// </summary>
    Task<ApiResult> PutAsync<TRequest>(string endpoint, TRequest request);

    /// <summary>
    /// Send a POST request without a body to the specified endpoint.
    /// </summary>
    Task<ApiResult> PostAsync(string endpoint);

    /// <summary>
    /// Send a DELETE request to the specified endpoint.
    /// </summary>
    Task<ApiResult> DeleteAsync(string endpoint);

    /// <summary>
    /// Send a PUT request without a body to the specified endpoint.
    /// </summary>
    Task<ApiResult> PutAsync(string endpoint);

    /// <summary>
    /// Send a POST request with multipart form data (for file uploads).
    /// </summary>
    Task<ApiResult<TResponse>> PostMultipartAsync<TResponse>(string endpoint, MultipartFormDataContent content);
}

/// <summary>
/// Result wrapper for API calls.
/// </summary>
public class ApiResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }

    public static ApiResult Success() => new() { IsSuccess = true, StatusCode = 200 };
    public static ApiResult Failure(string message, int statusCode = 400) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}

/// <summary>
/// Result wrapper for API calls with data.
/// </summary>
public class ApiResult<T> : ApiResult
{
    public T? Data { get; set; }

    public static ApiResult<T> Success(T data) => new() { IsSuccess = true, Data = data, StatusCode = 200 };
    public new static ApiResult<T> Failure(string message, int statusCode = 400) => new() { IsSuccess = false, ErrorMessage = message, StatusCode = statusCode };
}
