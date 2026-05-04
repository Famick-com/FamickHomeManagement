using System.Text.RegularExpressions;

namespace Famick.HomeManagement.Logging.Redaction.Redactors;

/// <summary>
/// Strips bearer-token and proxy-token header values from logged messages.
/// Catches both the literal <c>Authorization: Bearer ...</c> form (most common in
/// request logs) and the <c>X-ProxyToken</c> header used by the proxy add-on.
/// </summary>
public sealed partial class AuthorizationHeaderRedactor : IRedactor
{
    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var stage1 = AuthorizationHeader().Replace(input, "Authorization: <redacted>");
        return ProxyTokenHeader().Replace(stage1, "X-ProxyToken: <redacted>");
    }

    [GeneratedRegex(@"Authorization:\s*Bearer\s+[^\s,;""]+", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex(@"X-ProxyToken:\s*[^\s,;""]+", RegexOptions.IgnoreCase)]
    private static partial Regex ProxyTokenHeader();
}
