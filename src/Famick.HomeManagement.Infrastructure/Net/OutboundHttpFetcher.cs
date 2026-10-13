using System.Net;
using System.Net.Sockets;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Net;

/// <summary>
/// Settings for <see cref="OutboundHttpFetcher"/>, bound from the <c>OutboundFetch</c> section.
/// </summary>
public sealed class OutboundFetchOptions
{
    public const string SectionName = "OutboundFetch";

    /// <summary>
    /// Whether RFC 1918, CGNAT and IPv6 ULA addresses may be fetched, and ports other
    /// than 80/443. Null means the default for the host: allowed on a self-hosted
    /// server, refused on the multi-tenant cloud.
    /// </summary>
    public bool? AllowPrivateNetworks { get; set; }

    /// <summary>Whole-request budget: connect, redirects and body.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public int MaxRedirects { get; set; } = 5;
}

/// <summary>
/// The one way Famick fetches a URL a user typed in (issue #107; the product image
/// fetch, #108, is meant to move onto it too).
/// </summary>
/// <remarks>
/// The address check runs inside <see cref="SocketsHttpHandler.ConnectCallback"/>, on
/// the addresses the socket is actually about to connect to, so DNS rebinding between
/// a check and the connect has nothing to exploit. Redirects are followed by hand so
/// each hop's URL is checked too, and the proxy is disabled so the connect callback
/// always sees the real destination.
/// </remarks>
public sealed class OutboundHttpFetcher : IOutboundHttpFetcher
{
    public const string HttpClientName = "famick-outbound";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OutboundFetchOptions _options;
    private readonly bool _allowPrivateNetworks;
    private readonly ILogger<OutboundHttpFetcher> _logger;

    public OutboundHttpFetcher(
        IHttpClientFactory httpClientFactory,
        OutboundFetchOptions options,
        bool allowPrivateNetworks,
        ILogger<OutboundHttpFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _allowPrivateNetworks = allowPrivateNetworks;
        _logger = logger;
    }

    /// <summary>
    /// The primary handler for <see cref="HttpClientName"/>. Exposed so tests can drive
    /// the real connect-time check.
    /// </summary>
    public static SocketsHttpHandler CreateHandler(bool allowPrivateNetworks, TimeSpan connectTimeout) => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = connectTimeout,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = (context, cancellationToken) =>
            ConnectAsync(context.DnsEndPoint, allowPrivateNetworks, cancellationToken)
    };

    private static async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endPoint, bool allowPrivateNetworks, CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(endPoint.Host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken);

        // Refuse the whole host if any of its addresses is refused, rather than skipping
        // to the good ones: a name that answers with an internal address is not one we
        // want to talk to at all.
        if (addresses.Length == 0 || addresses.Any(a => !OutboundAddressPolicy.IsAllowed(a, allowPrivateNetworks)))
            throw new OutboundAddressRefusedException(endPoint.Host);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, endPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public OutboundFetchFailure? CheckUrl(Uri url)
    {
        if (!url.IsAbsoluteUri || string.IsNullOrEmpty(url.Host))
            return OutboundFetchFailure.UrlNotAllowed;
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
            return OutboundFetchFailure.UrlNotAllowed;
        if (!_allowPrivateNetworks && url.Port is not (80 or 443))
            return OutboundFetchFailure.UrlNotAllowed;
        return null;
    }

    public async Task<OutboundFetchResult> GetAsync(Uri url, long maxBytes, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            return await GetCoreAsync(url, maxBytes, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OutboundFetchException(OutboundFetchFailure.TimedOut);
        }
        catch (HttpRequestException ex) when (FindRefusal(ex) is { } refusal)
        {
            _logger.LogWarning("Outbound fetch refused: {Host} resolves to a disallowed address", refusal.Host);
            throw new OutboundFetchException(OutboundFetchFailure.AddressNotAllowed, inner: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new OutboundFetchException(OutboundFetchFailure.Unreachable, inner: ex);
        }
    }

    private async Task<OutboundFetchResult> GetCoreAsync(Uri url, long maxBytes, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        for (var hop = 0; ; hop++)
        {
            if (CheckUrl(url) is { } failure)
                throw new OutboundFetchException(failure);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (IsRedirect(response.StatusCode))
            {
                if (hop >= _options.MaxRedirects || response.Headers.Location is not { } location)
                    throw new OutboundFetchException(OutboundFetchFailure.TooManyRedirects);
                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new OutboundFetchException(OutboundFetchFailure.HttpError, (int)response.StatusCode);

            if (response.Content.Headers.ContentLength > maxBytes)
                throw new OutboundFetchException(OutboundFetchFailure.TooLarge);

            var body = await ReadCappedAsync(response.Content, maxBytes, cancellationToken);
            return new OutboundFetchResult(body, response.Content.Headers.ContentType?.ToString(), url);
        }
    }

    // Content-Length can be absent or wrong, and decompression changes the size anyway,
    // so the cap is enforced on the bytes actually read.
    private static async Task<byte[]> ReadCappedAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new OutboundFetchException(OutboundFetchFailure.TooLarge);
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static bool IsRedirect(HttpStatusCode status) => status is
        HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static OutboundAddressRefusedException? FindRefusal(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
            if (ex is OutboundAddressRefusedException refusal)
                return refusal;
        return null;
    }
}

internal sealed class OutboundAddressRefusedException(string host)
    : IOException($"Refusing to connect to {host}: it resolves to an address outbound fetches may not reach")
{
    public string Host { get; } = host;
}
