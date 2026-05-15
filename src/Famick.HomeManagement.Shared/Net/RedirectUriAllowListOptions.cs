namespace Famick.HomeManagement.Shared.Net;

/// <summary>
/// Configuration bound from the <c>RedirectUriAllowList</c> section of
/// <c>appsettings.json</c>. The allow-list is an explicit set of canonical
/// hostnames (no wildcards, no suffix matching — the Famick.AuthProxy design
/// doc rejects wildcards because a single subdomain takeover would turn into
/// a credential-stealer otherwise).
///
/// Example (cloud production):
/// <code>
/// "RedirectUriAllowList": {
///   "Hosts": ["app.famick.com", "auth.famick.com", "proxy.famick.com"]
/// }
/// </code>
/// </summary>
public class RedirectUriAllowListOptions
{
    public const string SectionName = "RedirectUriAllowList";

    /// <summary>
    /// Allow-listed hostnames. Compared exact-match (case-insensitive) against
    /// the canonical host produced by <see cref="UrlCanonicalizer"/>. An empty
    /// or missing list means no absolute URLs are accepted — only relative paths.
    /// </summary>
    public List<string> Hosts { get; set; } = new();
}
