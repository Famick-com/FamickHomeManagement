namespace Famick.HomeManagement.Shared.Net;

/// <summary>
/// Why <see cref="IRedirectUrlValidator.TryValidate"/> rejected a candidate
/// redirect URL. Each rejection kind is broken out so callers can decide how
/// to react: log + drop for server-to-client bounces, return 400 for direct
/// server <c>Redirect()</c> sinks, navigate-home + toast for SPA navigation.
/// </summary>
public enum RedirectRejectionReason
{
    /// <summary>Input was null or empty.</summary>
    Empty,

    /// <summary>Protocol-relative URL (<c>//evil.example/x</c>). Always rejected — the
    /// browser interprets these as same-scheme absolute URLs and they bypass naive
    /// "starts with /" checks.</summary>
    ProtocolRelative,

    /// <summary>Absolute URL whose canonical host is not on the configured allow-list.</summary>
    HostNotAllowed,

    /// <summary>Absolute URL that <see cref="UrlCanonicalizer"/> couldn't reduce to canonical
    /// form (userinfo, query, fragment, non-http scheme, malformed URI, etc.).
    /// Reject defensively — a malformed redirect target is never safe to honor.</summary>
    Malformed,
}
