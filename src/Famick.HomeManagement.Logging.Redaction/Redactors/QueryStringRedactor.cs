using System.Text.RegularExpressions;

namespace Famick.HomeManagement.Logging.Redaction.Redactors;

/// <summary>
/// Replaces query-string values with <c>&lt;redacted&gt;</c> while preserving the
/// keys (so logs still carry the shape of the URL for debugging).
///
/// A request like <c>GET /api/foo?api_key=abc&amp;sort=name</c> becomes
/// <c>GET /api/foo?api_key=&lt;redacted&gt;&amp;sort=&lt;redacted&gt;</c>.
/// We deliberately redact every value rather than maintaining an allow-list of
/// "safe" parameter names — values like sort orders are low-value to log, and the
/// allow-list-creep failure mode is silent.
/// </summary>
public sealed partial class QueryStringRedactor : IRedactor
{
    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return QueryParam().Replace(input, m => $"{m.Groups[1].Value}=<redacted>");
    }

    [GeneratedRegex(@"([?&][A-Za-z0-9_\-\.]+)=[^\s&""]+")]
    private static partial Regex QueryParam();
}
