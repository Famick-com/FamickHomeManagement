using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Calls the AuthProxy <c>POST /auth/lookup-email</c> endpoint so the
/// mobile app can resolve the email a user types on the sign-in page
/// to the specific home server they should sign in against. Used only
/// by proxied-mode setup; direct-mode (QR-scanned URL) skips this
/// entirely.
///
/// Uses its own <see cref="HttpClient"/> with a fixed base address —
/// the app's main client routes every request through
/// <see cref="DynamicApiHttpHandler"/>, which would rewrite our
/// AuthProxy URL into the wrong host. Singleton-scoped.
/// </summary>
public sealed class EmailLookupApi
{
    /// <summary>
    /// Production AuthProxy origin. Picked up from
    /// <see cref="ApiSettings.AuthProxyPublicBaseUrl"/> at construction
    /// time; kept here as a constant so callers don't pass it on every
    /// request.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;

    public EmailLookupApi()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(ApiSettings.AuthProxyPublicBaseUrl),
            Timeout = RequestTimeout,
        };
    }

    public async Task<EmailLookupOutcome> LookupAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return EmailLookupOutcome.NotFound;
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(
                "auth/lookup-email",
                new EmailLookupRequest { Email = email.Trim() },
                ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return EmailLookupOutcome.Network;
        }
        catch (HttpRequestException)
        {
            return EmailLookupOutcome.Network;
        }

        switch ((int)response.StatusCode)
        {
            case 200:
            {
                var body = await response.Content
                    .ReadFromJsonAsync<EmailLookupResponseDto>(cancellationToken: ct)
                    .ConfigureAwait(false);
                if (body is null || body.HomeServerId == Guid.Empty || string.IsNullOrWhiteSpace(body.BaseUrl))
                {
                    return EmailLookupOutcome.Network;
                }
                return EmailLookupOutcome.Found(new EmailLookupSuccess
                {
                    Email = email.Trim().ToLowerInvariant(),
                    HomeServerId = body.HomeServerId,
                    DisplayName = body.DisplayName ?? string.Empty,
                    BaseUrl = body.BaseUrl,
                });
            }

            case 404:
                return EmailLookupOutcome.NotFound;

            case 429:
                return EmailLookupOutcome.RateLimited;

            default:
                return EmailLookupOutcome.Network;
        }
    }

    private sealed class EmailLookupRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    private sealed class EmailLookupResponseDto
    {
        [JsonPropertyName("homeServerId")]
        public Guid HomeServerId { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; } = string.Empty;
    }
}

/// <summary>
/// Result of an <see cref="EmailLookupApi.LookupAsync"/> call. Outcome
/// shape is a tagged union — <see cref="Found"/> is the only success
/// path and carries the resolved home server; the rest convey enough
/// detail to render a specific error message without the caller having
/// to inspect HTTP status codes.
/// </summary>
public readonly record struct EmailLookupOutcome(EmailLookupOutcomeKind Kind, EmailLookupSuccess? Result)
{
    public static EmailLookupOutcome Found(EmailLookupSuccess result) => new(EmailLookupOutcomeKind.Found, result);
    public static readonly EmailLookupOutcome NotFound = new(EmailLookupOutcomeKind.NotFound, null);
    public static readonly EmailLookupOutcome RateLimited = new(EmailLookupOutcomeKind.RateLimited, null);
    public static readonly EmailLookupOutcome Network = new(EmailLookupOutcomeKind.Network, null);
}

public enum EmailLookupOutcomeKind
{
    Found,
    NotFound,
    RateLimited,
    Network,
}

public sealed class EmailLookupSuccess
{
    public string Email { get; init; } = string.Empty;
    public Guid HomeServerId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
}
