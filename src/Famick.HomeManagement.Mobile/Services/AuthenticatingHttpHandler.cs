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
    private static readonly SemaphoreSlim StepUpSemaphore = new(1, 1);
    private static readonly TimeSpan StepUpTimeout = TimeSpan.FromMinutes(5);

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

        // Handle 403 responses — check for specific error types before falling through
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var forbiddenContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Must change password — redirect to force change password page
            if (forbiddenContent.Contains("MUST_CHANGE_PASSWORD", StringComparison.Ordinal))
            {
                Console.WriteLine("[AuthHandler] Must change password 403 — sending MustChangePasswordMessage");
                WeakReferenceMessenger.Default.Send(new MustChangePasswordMessage("Server requires password change"));
                return response;
            }

            // Must accept terms — redirect to accept terms page
            if (forbiddenContent.Contains("MUST_ACCEPT_TERMS", StringComparison.Ordinal))
            {
                Console.WriteLine("[AuthHandler] Must accept terms 403 — sending MustAcceptTermsMessage");
                WeakReferenceMessenger.Default.Send(new MustAcceptTermsMessage("Server requires terms acceptance"));
                return response;
            }

            // Subscription tier errors should NOT trigger token refresh or logout.
            // Return them directly so the UI can show an upgrade prompt.
            if (forbiddenContent.Contains("SUBSCRIPTION_TIER_INSUFFICIENT", StringComparison.OrdinalIgnoreCase)
                || forbiddenContent.Contains("SUBSCRIPTION_EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[AuthHandler] Subscription tier 403 — returning directly (no refresh)");
                return response;
            }

            // Phase 2.5 — step-up reauth: ask the UI to show a reauth modal,
            // wait for a new access token, then retry the original request.
            if (forbiddenContent.Contains("STEP_UP_REQUIRED", StringComparison.Ordinal))
            {
                var stepUpRetry = await TryStepUpRetryAsync(request, cancellationToken).ConfigureAwait(false);
                if (stepUpRetry != null)
                {
                    response.Dispose();
                    return stepUpRetry;
                }
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

    /// <summary>
    /// Phase 2.5 — broadcast a <see cref="StepUpRequiredMessage"/> with a TCS,
    /// wait for the UI's reauth modal to complete it with either a fresh
    /// access token or null (cancel). On token: swap into storage, clone the
    /// original request, retry. Otherwise return null so the caller surfaces
    /// the original 403.
    ///
    /// Wrapped in a semaphore so concurrent [StepUp]-gated requests share a
    /// single modal — the first request opens it, subsequent requests see the
    /// new token already in storage and retry without re-prompting.
    /// </summary>
    private async Task<HttpResponseMessage?> TryStepUpRetryAsync(
        HttpRequestMessage originalRequest,
        CancellationToken cancellationToken)
    {
        // Capture the access token at entry so we can detect "another thread
        // already step-upped" inside the semaphore (analogous to TryRefresh).
        var tokenBeforeStepUp = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);

        var acquired = await StepUpSemaphore.WaitAsync(StepUpTimeout, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            Console.WriteLine("[AuthHandler] Step-up semaphore timeout");
            return null;
        }

        try
        {
            // If another thread already completed step-up, just retry with the
            // current token — no second modal needed.
            var currentToken = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(currentToken) && currentToken != tokenBeforeStepUp)
            {
                Console.WriteLine("[AuthHandler] Step-up already completed by another request");
                return await RetryWithTokenAsync(originalRequest, currentToken!, cancellationToken).ConfigureAwait(false);
            }

            // Open the modal via WeakReferenceMessenger; the UI completes the TCS.
            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Console.WriteLine("[AuthHandler] STEP_UP_REQUIRED 403 — sending StepUpRequiredMessage");
            WeakReferenceMessenger.Default.Send(new StepUpRequiredMessage(tcs));

            // Bound the wait so a hung modal doesn't block the request forever.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(StepUpTimeout);

            string? newToken;
            try
            {
                newToken = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[AuthHandler] Step-up timed out waiting for modal");
                return null;
            }

            if (string.IsNullOrEmpty(newToken))
            {
                Console.WriteLine("[AuthHandler] Step-up cancelled by user");
                return null;
            }

            // The modal is responsible for writing the new token to storage; we
            // re-read it here rather than trusting the TCS payload alone.
            var storedToken = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
            var tokenToUse = !string.IsNullOrEmpty(storedToken) ? storedToken! : newToken;

            return await RetryWithTokenAsync(originalRequest, tokenToUse, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            StepUpSemaphore.Release();
        }
    }

    private async Task<HttpResponseMessage> RetryWithTokenAsync(
        HttpRequestMessage originalRequest,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var retryRequest = await CloneRequestAsync(originalRequest).ConfigureAwait(false);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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
                // No refresh token and no access token means there was never a session here:
                // this is an unauthenticated call made while signed out, not one that expired.
                // Reporting expiry would clear storage and throw the user to the login screen —
                // which, during registration, discards a signup they are part-way through.
                if (string.IsNullOrEmpty(currentToken))
                {
                    Console.WriteLine("[AuthHandler] 401 with no session — not signalling expiry");
                    return false;
                }

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

        // Endpoints under api/auth/ that require [Authorize] must send the token:
        if (path.Contains("api/auth/external/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Contains("api/auth/accept-terms", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Contains("api/auth/logout", StringComparison.OrdinalIgnoreCase))
            return false;
        // Phase 2.5 — reauth is the step-up endpoint; the user is already
        // logged in and the controller has [Authorize]. Without attaching
        // the bearer token here, /api/auth/reauth always returns 401 and
        // the step-up modal can never succeed.
        if (path.Contains("api/auth/reauth", StringComparison.OrdinalIgnoreCase))
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

    private sealed class RefreshTokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }
}
