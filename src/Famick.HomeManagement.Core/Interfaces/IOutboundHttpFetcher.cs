namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Fetches a URL a user supplied. Every address the request connects to, including
/// each redirect hop, is checked against the outbound address policy at connect time,
/// so a hostname that resolves (or re-resolves) to an internal address is refused.
/// The fetch is bounded in time and size.
/// </summary>
public interface IOutboundHttpFetcher
{
    /// <summary>
    /// Checks the URL's shape (scheme, port, host) without touching the network.
    /// Returns null when it may be fetched, otherwise the reason it may not.
    /// </summary>
    OutboundFetchFailure? CheckUrl(Uri url);

    /// <summary>
    /// GETs <paramref name="url"/>, following up to a few redirects, and returns the
    /// body. Throws <see cref="OutboundFetchException"/> on any refusal or failure.
    /// </summary>
    Task<OutboundFetchResult> GetAsync(Uri url, long maxBytes, CancellationToken cancellationToken = default);
}

public sealed record OutboundFetchResult(byte[] Body, string? ContentType, Uri FinalUrl);

public enum OutboundFetchFailure
{
    /// <summary>Not http/https, a disallowed port, or no host.</summary>
    UrlNotAllowed,
    /// <summary>The host resolved to an address the policy refuses.</summary>
    AddressNotAllowed,
    TooManyRedirects,
    /// <summary>The server answered with a non-success status.</summary>
    HttpError,
    TooLarge,
    TimedOut,
    /// <summary>DNS failure, refused connection, TLS failure and the like.</summary>
    Unreachable
}

public sealed class OutboundFetchException : Exception
{
    public OutboundFetchFailure Failure { get; }

    /// <summary>Set when <see cref="Failure"/> is <see cref="OutboundFetchFailure.HttpError"/>.</summary>
    public int? StatusCode { get; }

    public OutboundFetchException(OutboundFetchFailure failure, int? statusCode = null, Exception? inner = null)
        : base($"Outbound fetch failed: {failure}{(statusCode is { } s ? $" ({s})" : "")}", inner)
    {
        Failure = failure;
        StatusCode = statusCode;
    }
}
