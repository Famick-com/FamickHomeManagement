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
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // Attach current access token
        var accessToken = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Subscription tier errors (403) should NOT trigger token refresh or logout.
        // Return them directly so the UI can show an upgrade prompt.
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            if (await IsSubscriptionError(response).ConfigureAwait(false))
            {
                Console.WriteLine("[AuthHandler] Subscription tier 403 — returning directly (no refresh)");
                return response;
            }
        }

        // 402 Payment Required (subscription expired) — return directly
        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            Console.WriteLine("[AuthHandler] Subscription expired 402 — returning directly");
            return response;
        }

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // 401 received — attempt token refresh
        Console.WriteLine("[AuthHandler] 401 received, attempting token refresh");

        var refreshed = await TryRefreshTokenAsync(accessToken, cancellationToken).ConfigureAwait(false);
        if (!refreshed)
        {
            return response;
        }

        // Retry the original request with the new token
        var newToken = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
        var retryRequest = await CloneRequestAsync(request).ConfigureAwait(false);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

        return await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TryRefreshTokenAsync(string? tokenBeforeRefresh, CancellationToken cancellationToken)
    {
        var acquired = await RefreshSemaphore.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
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
                await HandleRefreshFailureAsync().ConfigureAwait(false);
                return false;
            }

            // Build refresh request
            var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
            {
                Content = JsonContent.Create(new { refreshToken })
            };

            var response = await base.SendAsync(refreshRequest, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponseDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
                if (result != null && !string.IsNullOrEmpty(result.AccessToken) && !string.IsNullOrEmpty(result.RefreshToken))
                {
                    await _tokenStorage.SetTokensAsync(result.AccessToken, result.RefreshToken).ConfigureAwait(false);
                    Console.WriteLine("[AuthHandler] Token refreshed successfully");
                    return true;
                }
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Console.WriteLine($"[AuthHandler] Refresh failed with {response.StatusCode} — session expired");
                await HandleRefreshFailureAsync().ConfigureAwait(false);
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
        await _tokenStorage.ClearTokensAsync().ConfigureAwait(false);
        WeakReferenceMessenger.Default.Send(new SessionExpiredMessage("Refresh token expired or revoked"));
    }

    private static bool IsAuthEndpoint(string path)
    {
        if (path.Equals("health", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/health", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!path.Contains("api/auth/", StringComparison.OrdinalIgnoreCase))
            return false;

        // Endpoints under api/auth/external/ that require [Authorize] must send the token:
        // - GET  linked accounts, DELETE unlink, POST link, POST native/link
        if (path.Contains("api/auth/external/", StringComparison.OrdinalIgnoreCase))
            return false;

        // All other api/auth/ paths (login, register, refresh, challenge, config) are anonymous
        return true;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content != null)
        {
            var content = await original.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
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

    /// <summary>
    /// Checks if a 403 response is a subscription tier error (not a session/permission error).
    /// </summary>
    private static async Task<bool> IsSubscriptionError(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content.Contains("SUBSCRIPTION_TIER_INSUFFICIENT", StringComparison.OrdinalIgnoreCase)
                || content.Contains("SUBSCRIPTION_EXPIRED", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private sealed class RefreshTokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }
}
