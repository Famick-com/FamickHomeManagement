namespace Famick.HomeManagement.Shared.Net;

/// <summary>
/// Validates user-supplied redirect targets against the configured allow-list.
/// Phase 3 chunk 3.B. Closes the open-redirect surface on every <c>returnUrl</c>
/// / <c>ReturnUrl</c> sink in the codebase — three on web today (login form
/// redirect, external-auth callback redirect, ASP.NET <c>FormPostCallback</c>),
/// more coming with Phase 4–5.
///
/// Accepts:
/// - Relative paths (anything starting with <c>/</c> but not <c>//</c>).
/// - Absolute URLs whose canonical host (per <see cref="UrlCanonicalizer"/>)
///   exactly matches an entry in <see cref="RedirectUriAllowListOptions.Hosts"/>.
///
/// Rejects:
/// - Empty / null input.
/// - Protocol-relative URLs (<c>//evil.example/x</c>) — browser-side these are
///   same-scheme absolute and bypass "starts with /" naive checks.
/// - Absolute URLs to any host not on the allow-list (including
///   subdomain-takeover strings like <c>app.famick.com.evil.example</c>).
/// - Anything <see cref="UrlCanonicalizer"/> rejects (userinfo, query,
///   fragment, non-http scheme, malformed).
/// </summary>
public interface IRedirectUrlValidator
{
    /// <summary>
    /// Validates <paramref name="input"/>. On success, returns <c>true</c> with
    /// <paramref name="safeUrl"/> set to either:
    /// - the input unchanged, if it was a relative path; or
    /// - the canonical form of the input, if it was an allow-listed absolute URL.
    /// On failure, returns <c>false</c> and sets <paramref name="reason"/>.
    /// </summary>
    bool TryValidate(string? input, out string? safeUrl, out RedirectRejectionReason reason);
}
