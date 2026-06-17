using Microsoft.AspNetCore.Components;

namespace Famick.HomeManagement.UI;

/// <summary>
/// Navigation helpers that keep in-app navigation working when the app is
/// hosted under a path prefix (e.g. Home Assistant Ingress at
/// <c>/api/hassio_ingress/&lt;token&gt;/</c>) as well as at the site root.
/// </summary>
/// <remarks>
/// The app historically navigated with root-absolute paths
/// (<c>NavigateTo("/login")</c>). A leading slash resolves against the host
/// origin, ignoring the document <c>&lt;base href&gt;</c> — fine at the root,
/// but under Ingress it escapes the prefix to <c>https://ha/login</c>, which
/// Supervisor doesn't route to the add-on (404). The Blazor-correct form is a
/// base-relative path (no leading slash) so it resolves against
/// <c>&lt;base href&gt;</c>. <see cref="NavTo"/> converts an app-absolute path
/// to base-relative at the navigation boundary, so call sites can keep using
/// the familiar <c>/path</c> spelling and the validators that gate
/// <c>returnUrl</c> can keep requiring a leading slash, while what actually
/// reaches the browser is base-relative and works in both hosting modes.
/// </remarks>
public static class NavigationManagerExtensions
{
    /// <summary>
    /// Like <see cref="NavigationManager.NavigateTo(string, bool, bool)"/> but
    /// rewrites an app-absolute path (<c>/foo</c>) to base-relative (<c>foo</c>)
    /// so it honors <c>&lt;base href&gt;</c>. Absolute and protocol-relative
    /// URLs pass through untouched.
    /// </summary>
    public static void NavTo(this NavigationManager navigation, string uri, bool forceLoad = false, bool replace = false)
        => navigation.NavigateTo(ToNavTarget(uri), forceLoad, replace);

    /// <summary>
    /// Pure path transform behind <see cref="NavTo"/>. App-absolute internal
    /// paths lose their single leading slash; <c>"/"</c> becomes <c>""</c>
    /// (home, resolved against the base href). Absolute URLs (<c>scheme://…</c>)
    /// and protocol-relative URLs (<c>//host/…</c>) — which intentionally leave
    /// the app — and already-relative paths are returned unchanged.
    /// </summary>
    public static string ToNavTarget(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return uri;
        }

        // Leaves the app: hand to NavigationManager verbatim.
        if (uri.StartsWith("//", StringComparison.Ordinal)
            || uri.Contains("://", StringComparison.Ordinal))
        {
            return uri;
        }

        // App-absolute internal path -> base-relative so <base href> applies.
        if (uri[0] == '/')
        {
            return uri.Length == 1 ? string.Empty : uri[1..];
        }

        // Already relative.
        return uri;
    }
}
