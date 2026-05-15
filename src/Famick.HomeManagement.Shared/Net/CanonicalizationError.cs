namespace Famick.HomeManagement.Shared.Net;

/// <summary>
/// Why <see cref="UrlCanonicalizer.TryCanonicalize"/> rejected an input. Maps
/// 1:1 to the validation rules from the Famick.AuthProxy design doc — callers
/// translate these into HTTP 400 responses with the specific reason. The doc
/// requires <c>scheme://host:port</c> only, with userinfo / query / fragment /
/// path each rejected on their own merits (not collapsed into a generic
/// "malformed"), so audits can tell whether an operator pasted credentials,
/// a tracking query, or just a wrong URL.
/// </summary>
public enum CanonicalizationError
{
    /// <summary>Input was null or empty whitespace.</summary>
    EmptyInput,

    /// <summary>Input could not be parsed as an absolute URI.</summary>
    InvalidUri,

    /// <summary>Scheme is neither <c>http</c> nor <c>https</c>.</summary>
    UnsupportedScheme,

    /// <summary>URI authority contains userinfo (anything before the <c>@</c>).</summary>
    UserInfoNotAllowed,

    /// <summary>Host portion is empty after parsing (e.g. <c>https:///</c>).</summary>
    EmptyHost,

    /// <summary>URI has a non-root path. Canonical form is <c>scheme://host:port</c> only.</summary>
    PathNotAllowed,

    /// <summary>URI has a query string.</summary>
    QueryNotAllowed,

    /// <summary>URI has a fragment.</summary>
    FragmentNotAllowed,
}
