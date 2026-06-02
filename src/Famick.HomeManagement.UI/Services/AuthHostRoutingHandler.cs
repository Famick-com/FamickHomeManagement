namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Phase 5 chunk 5.K — DelegatingHandler that rewrites the host of outgoing
/// auth requests to <see cref="AuthCloudUrl"/> when
/// <see cref="IAuthHostFlagStorage"/> reports the
/// <c>use_auth_famick_com</c> flag is on. Symmetric to the mobile
/// <c>DynamicApiHttpHandler</c> introduced in chunk 5.J — covers login,
/// refresh, config, challenge, callback, passkey, reauth, logout, register,
/// forgot-password, and the root <c>/check</c> endpoint in one seam so no
/// per-call-site swap is needed.
///
/// The flag-storage call returns false by default (e.g. when no storage is
/// registered in self-hosted), so the handler is a no-op until the SPA
/// fetches the server config and persists the flag.
/// </summary>
public class AuthHostRoutingHandler : DelegatingHandler
{
    public const string AuthCloudUrl = "https://auth.famick.com";

    private readonly IAuthHostFlagStorage _flagStorage;

    public AuthHostRoutingHandler(IAuthHostFlagStorage flagStorage)
    {
        _flagStorage = flagStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri != null && request.RequestUri.IsAbsoluteUri)
        {
            var path = request.RequestUri.AbsolutePath.TrimStart('/');
            if (IsAuthPath(path))
            {
                var useAuthHost = await _flagStorage.GetUseAuthFamickComAsync().ConfigureAwait(false);
                if (useAuthHost)
                {
                    var rewritten = new UriBuilder(AuthCloudUrl)
                    {
                        Path = request.RequestUri.AbsolutePath,
                        Query = request.RequestUri.Query.TrimStart('?'),
                    };
                    request.RequestUri = rewritten.Uri;
                }
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Matches every endpoint served by <c>Famick.HomeManagement.Web.Shared</c>'s
    /// auth controllers (<c>AuthApiController</c>, <c>ExternalAuthApiController</c>,
    /// <c>PasskeyApiController</c>, <c>JwksController</c>) plus the root
    /// <c>/check</c> endpoint (<c>CheckController</c>, promoted to Web.Shared in
    /// chunk 5.B). All of these are served byte-equally by both hosts so the
    /// swap is transparent at the protocol level.
    /// </summary>
    internal static bool IsAuthPath(string path)
    {
        return path.StartsWith("api/auth/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("check", StringComparison.OrdinalIgnoreCase)
            || path.Equals(".well-known/jwks.json", StringComparison.OrdinalIgnoreCase);
    }
}
