using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Shared.Net;

/// <inheritdoc cref="IRedirectUrlValidator"/>
public sealed class RedirectUrlValidator : IRedirectUrlValidator
{
    private readonly HashSet<string> _allowedHosts;

    public RedirectUrlValidator(IOptions<RedirectUriAllowListOptions> options)
    {
        // Build the allow-list as a canonical, case-insensitive set so the
        // lookup at validate-time is O(1) and matches independent of how the
        // operator typed the hostnames in appsettings (trailing whitespace,
        // mixed case, etc.).
        _allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var host in options.Value.Hosts ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(host)) continue;
            _allowedHosts.Add(host.Trim());
        }
    }

    public bool TryValidate(string? input, out string? safeUrl, out RedirectRejectionReason reason)
    {
        safeUrl = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            reason = RedirectRejectionReason.Empty;
            return false;
        }

        // Protocol-relative `//host/path` — browser interprets as same-scheme
        // absolute. Check BEFORE the "starts with /" relative-path branch
        // because both start with "/".
        if (input.StartsWith("//", StringComparison.Ordinal))
        {
            reason = RedirectRejectionReason.ProtocolRelative;
            return false;
        }

        // Relative path: anything starting with `/` and not `//`. Pass through
        // unchanged — the caller's NavigationManager / Redirect call resolves
        // it against the current origin, which is by definition trusted.
        if (input.StartsWith('/'))
        {
            safeUrl = input;
            reason = default;
            return true;
        }

        // Absolute URL: canonicalize first, then exact-match the host against
        // the allow-list. UrlCanonicalizer also rejects userinfo / path /
        // query / fragment — all of which we want rejected for redirect
        // targets too. A path-bearing URL like
        // https://app.famick.com/dashboard would canonicalize-reject, which
        // is acceptable: the only legitimate callers that need a path past
        // this gate are the SPA navigating from a relative path (handled
        // above) or a system controller building its own absolute Location
        // header (which never feeds through this validator).
        if (!UrlCanonicalizer.TryCanonicalize(input, out var canonical, out _))
        {
            reason = RedirectRejectionReason.Malformed;
            return false;
        }

        // Extract host from canonical scheme://host[:port]. Faster than
        // re-parsing via Uri since canonical is guaranteed well-formed.
        var hostStart = canonical.IndexOf("://", StringComparison.Ordinal) + 3;
        var portStart = canonical.IndexOf(':', hostStart);
        var host = portStart < 0
            ? canonical[hostStart..]
            : canonical[hostStart..portStart];

        // IPv6 hosts are bracketed in canonical form (`[::1]`); strip the
        // brackets for the allow-list compare so operators can list
        // `::1` or `[::1]` and either works.
        if (host.StartsWith('[') && host.EndsWith(']'))
        {
            host = host[1..^1];
        }

        if (!_allowedHosts.Contains(host))
        {
            reason = RedirectRejectionReason.HostNotAllowed;
            return false;
        }

        safeUrl = canonical;
        reason = default;
        return true;
    }
}
