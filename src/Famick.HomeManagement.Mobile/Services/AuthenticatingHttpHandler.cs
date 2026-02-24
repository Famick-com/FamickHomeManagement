using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// DelegatingHandler that transparently attaches the access token to outgoing requests
/// and refreshes it on 401 responses. Sits between HttpClient and DynamicApiHttpHandler.
/// </summary>
public class AuthenticatingHttpHandler : DelegatingHandler
{
    private readonly TokenStorage _tokenStorage;
    private readonly ApiSettings _apiSettings;
    private static readonly SemaphoreSlim RefreshSemaphore = new(1, 1);

    public AuthenticatingHttpHandler(TokenStorage tokenStorage, ApiSettings apiSettings)
    {
        _tokenStorage = tokenStorage;
        _apiSettings = apiSettings;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.OriginalString ?? request.RequestUri?.PathAndQuery ?? "";

        // Skip token attachment for auth and health endpoints to prevent infinite loops
        if (IsAuthEndpoint(path))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Attach current access token
        var accessToken = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // 401 received — attempt token refresh
        Console.WriteLine("[AuthHandler] 401 received, attempting token refresh");

        var refreshed = await TryRefreshTokenAsync(accessToken, cancellationToken);
        if (!refreshed)
        {
            return response;
        }

        // Retry the original request with the new token
        var newToken = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
        var retryRequest = await CloneRequestAsync(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private async Task<bool> TryRefreshTokenAsync(string? tokenBeforeRefresh, CancellationToken cancellationToken)
    {
        var acquired = await RefreshSemaphore.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        if (!acquired)
        {
            return false;
        }

        try
        {
            // Double-check: if another thread already refreshed, the stored token will differ
            var currentToken = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(currentToken) && currentToken != tokenBeforeRefresh)
            {
                Console.WriteLine("[AuthHandler] Token already refreshed by another thread");
                return true;
            }

            var refreshToken = await _tokenStorage.GetRefreshTokenAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(refreshToken))
            {
                Console.WriteLine("[AuthHandler] No refresh token available");
                await HandleRefreshFailureAsync();
                return false;
            }

            // Build refresh request
            var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
            {
                Content = JsonContent.Create(new { refreshToken })
            };

            var response = await base.SendAsync(refreshRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponseDto>(cancellationToken: cancellationToken);
                if (result != null && !string.IsNullOrEmpty(result.AccessToken) && !string.IsNullOrEmpty(result.RefreshToken))
                {
                    await _tokenStorage.SetTokensAsync(result.AccessToken, result.RefreshToken);
                    Console.WriteLine("[AuthHandler] Token refreshed successfully");
                    return true;
                }
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Console.WriteLine($"[AuthHandler] Refresh failed with {response.StatusCode} — session expired");
                await HandleRefreshFailureAsync();
            }
            else
            {
                // Transient error (network issue, 500, etc.) — do NOT send SessionExpiredMessage
                Console.WriteLine($"[AuthHandler] Refresh failed with {response.StatusCode} — transient error, not logging out");
            }

            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Network-level failure — do NOT send SessionExpiredMessage
            Console.WriteLine($"[AuthHandler] Refresh exception (transient): {ex.Message}");
            return false;
        }
        finally
        {
            RefreshSemaphore.Release();
        }
    }

    private async Task HandleRefreshFailureAsync()
    {
        await _tokenStorage.ClearTokensAsync();
        WeakReferenceMessenger.Default.Send(new SessionExpiredMessage("Refresh token expired or revoked"));
    }

    private static bool IsAuthEndpoint(string path)
    {
        return path.Contains("api/auth/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("health", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/health", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content != null)
        {
            var content = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var prop in original.Options)
        {
            clone.Options.TryAdd(prop.Key, prop.Value);
        }

        return clone;
    }

    private sealed class RefreshTokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }
}
