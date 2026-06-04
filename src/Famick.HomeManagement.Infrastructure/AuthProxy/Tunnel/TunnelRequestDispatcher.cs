using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;

public sealed class TunnelRequestDispatcher : ITunnelRequestDispatcher
{
    /// <summary>
    /// Name of the named <see cref="IHttpClientFactory"/> client used
    /// for loopback dispatch. Configured in Program.cs with a handler
    /// that bypasses cert validation (loopback only — dev cert is
    /// self-signed).
    /// </summary>
    public const string HttpClientName = "AuthProxyTunnelLoopback";

    /// <summary>
    /// Hop-by-hop headers that must NOT be copied from the incoming
    /// envelope onto the outbound HttpRequestMessage, nor mirrored
    /// from the loopback response back into the envelope. They're
    /// connection-scoped and would confuse the loopback HttpClient
    /// (Content-Length in particular is computed automatically).
    /// </summary>
    private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Connection", "Upgrade", "Keep-Alive", "Proxy-Connection",
        "Proxy-Authorization", "Transfer-Encoding", "TE", "Trailer",
        "Content-Length",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TunnelRequestDispatcher> _logger;

    public TunnelRequestDispatcher(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TunnelRequestDispatcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HttpResponseFrame> DispatchAsync(HttpRequestFrame request, CancellationToken ct)
    {
        var baseUrl = ResolveLoopbackBaseUrl();
        var url = baseUrl.TrimEnd('/') + EnsureLeadingSlash(request.Path);

        using var outbound = new HttpRequestMessage(new HttpMethod(request.Method), url);
        if (!string.IsNullOrEmpty(request.BodyBase64))
        {
            outbound.Content = new ByteArrayContent(Convert.FromBase64String(request.BodyBase64));
        }

        foreach (var (header, values) in request.Headers)
        {
            if (HopByHop.Contains(header)) continue;
            if (!outbound.Headers.TryAddWithoutValidation(header, values) && outbound.Content is not null)
            {
                outbound.Content.Headers.TryAddWithoutValidation(header, values);
            }
        }

        var http = _httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(outbound, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Tunnel loopback dispatch failed for request_id={RequestId} url={Url}",
                request.RequestId, url);
            return new HttpResponseFrame(
                request.RequestId,
                Status: 502,
                Headers: new Dictionary<string, string[]>(),
                BodyBase64: null);
        }

        try
        {
            var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, values) in response.Headers)
            {
                if (HopByHop.Contains(name)) continue;
                headers[name] = values.ToArray();
            }
            foreach (var (name, values) in response.Content.Headers)
            {
                if (HopByHop.Contains(name)) continue;
                headers[name] = values.ToArray();
            }

            var bodyBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return new HttpResponseFrame(
                request.RequestId,
                Status: (int)response.StatusCode,
                Headers: headers,
                BodyBase64: bodyBytes.Length > 0 ? Convert.ToBase64String(bodyBytes) : null);
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// Returns the URL the loopback HttpClient should hit. Preference:
    /// <list type="number">
    ///   <item>Explicit <c>AuthProxy:LoopbackBaseUrl</c> in config — set by
    ///         the host in Program.cs once Kestrel has bound (so it can
    ///         read the actual port out of <c>IServerAddressesFeature</c>).</item>
    ///   <item>Whatever <c>ASPNETCORE_URLS</c> looked like at boot — the
    ///         host's pre-binding hint.</item>
    ///   <item>Hard-coded <c>http://localhost:5003</c> dev default.</item>
    /// </list>
    /// </summary>
    private string ResolveLoopbackBaseUrl()
    {
        var configured = _configuration["AuthProxy:LoopbackBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var aspnetUrls = _configuration["ASPNETCORE_URLS"];
        if (!string.IsNullOrWhiteSpace(aspnetUrls))
        {
            // ASPNETCORE_URLS can be ";"-separated. Prefer http; fall back
            // to https (the loopback HttpClient has cert validation off).
            var candidates = aspnetUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var picked =
                candidates.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            if (picked is not null)
            {
                return NormaliseLoopback(picked);
            }
        }

        return "http://localhost:5003";
    }

    private static string NormaliseLoopback(string address)
    {
        // Replace wildcard hosts with the loopback explicitly so HttpClient
        // can connect. "http://*:5003" / "http://+:5003" / "http://[::]:5003"
        // all become "http://localhost:5003".
        return address
            .Replace("://*", "://localhost", StringComparison.Ordinal)
            .Replace("://+", "://localhost", StringComparison.Ordinal)
            .Replace("://[::]", "://localhost", StringComparison.Ordinal)
            .Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal);
    }

    private static string EnsureLeadingSlash(string path) =>
        string.IsNullOrEmpty(path) ? "/" : (path.StartsWith('/') ? path : "/" + path);
}
