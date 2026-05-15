namespace Famick.HomeManagement.Shared.Net;

/// <summary>
/// Phase 3 chunk 3.A. Reduces any input URL to a strict canonical form
/// <c>scheme://host[:port]</c> — no userinfo, no path, no query, no fragment,
/// host lower-cased (IDN handled via <see cref="Uri.IdnHost"/>), default ports
/// (80/443) elided. The output is suitable as a stable key for: the open-redirect
/// allow-list comparison (chunk 3.B), the <c>local_url</c> validation at proxy
/// sign-up (Phase 8), and the <c>localServer</c> change-detection compare on
/// every login response (Phase 6). All three of those sites need the same
/// definition of "same URL" so a no-op rewrite (trailing slash, casing, default
/// port) doesn't trigger a false-positive prompt or rejection.
///
/// Design notes:
/// - Result-type API (<see cref="TryCanonicalize"/>) — callers translate
///   the <see cref="CanonicalizationError"/> into the appropriate 400 message
///   per the design doc's section §2 "Local URL validation". Exception-as-flow
///   would lose the reason. <see cref="CanonicalizeOrNull"/> is sugar for
///   callers that only need the happy-path string.
/// - Strict authority parsing — userinfo / path / query / fragment each map
///   to their own error so audits can tell whether an operator pasted
///   credentials, a tracking query, or just the wrong URL.
/// - No IDN re-encoding fight — we always emit <see cref="Uri.IdnHost"/>
///   (punycode for non-ASCII hosts), which is what every downstream verifier
///   compares against.
/// </summary>
public static class UrlCanonicalizer
{
    /// <summary>
    /// Reduces <paramref name="input"/> to canonical <c>scheme://host[:port]</c>
    /// form. Returns <c>true</c> with <paramref name="canonical"/> set on success.
    /// On failure, returns <c>false</c> and sets <paramref name="error"/> to the
    /// specific reason.
    /// </summary>
    public static bool TryCanonicalize(string? input, out string canonical, out CanonicalizationError error)
    {
        canonical = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = CanonicalizationError.EmptyInput;
            return false;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            error = CanonicalizationError.InvalidUri;
            return false;
        }

        // Scheme allow-list. Limit to http/https — file://, ftp://, javascript:,
        // and any custom scheme are out of scope for every Phase 3+ consumer.
        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not ("http" or "https"))
        {
            error = CanonicalizationError.UnsupportedScheme;
            return false;
        }

        if (uri.UserInfo.Length > 0)
        {
            error = CanonicalizationError.UserInfoNotAllowed;
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            error = CanonicalizationError.EmptyHost;
            return false;
        }

        // Reject any path beyond the implicit root "/". Uri.AbsolutePath is
        // always at least "/" for an authority-bearing URL, so the check is
        // "anything more than the root slash".
        if (uri.AbsolutePath.Length > 1)
        {
            error = CanonicalizationError.PathNotAllowed;
            return false;
        }

        if (uri.Query.Length > 0)
        {
            error = CanonicalizationError.QueryNotAllowed;
            return false;
        }

        if (uri.Fragment.Length > 0)
        {
            error = CanonicalizationError.FragmentNotAllowed;
            return false;
        }

        // Host: lower-case, punycode for IDN, bracketed for IPv6.
        var host = uri.HostNameType == UriHostNameType.IPv6
            ? $"[{uri.IdnHost}]"
            : uri.IdnHost.ToLowerInvariant();

        // Drop the default port for the scheme.
        var port = uri.Port;
        var defaultPort = scheme == "https" ? 443 : 80;
        var portSuffix = (port == defaultPort || port == -1) ? string.Empty : $":{port}";

        canonical = $"{scheme}://{host}{portSuffix}";
        error = default;
        return true;
    }

    /// <summary>
    /// Sugar for callers that don't need to distinguish error reasons. Returns
    /// the canonical form on success or <c>null</c> on any rejection. Prefer
    /// <see cref="TryCanonicalize"/> when producing user-facing 400 errors.
    /// </summary>
    public static string? CanonicalizeOrNull(string? input)
        => TryCanonicalize(input, out var canonical, out _) ? canonical : null;
}
