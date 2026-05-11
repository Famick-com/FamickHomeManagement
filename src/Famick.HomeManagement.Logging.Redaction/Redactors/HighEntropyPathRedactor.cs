using System.Text.RegularExpressions;

namespace Famick.HomeManagement.Logging.Redaction.Redactors;

/// <summary>
/// Redacts path segments that look like high-entropy tokens (share-link tokens,
/// calendar ICS tokens, etc.). Catches new tokenized routes added in future phases
/// without requiring the redaction list to grow per-route.
///
/// Rule: any path segment matching <c>/[A-Za-z0-9_-]{24,}</c> (base64url-ish, ≥24 chars)
/// or <c>/[a-fA-F0-9]{16,}</c> (hex-ish, ≥16 chars) is replaced with <c>/&lt;redacted&gt;</c>.
/// Lower thresholds would catch UUIDs (which are 32 hex with dashes) and routine path
/// IDs; the chosen thresholds are deliberately generous to avoid false positives on
/// resource IDs and route slugs.
/// </summary>
public sealed partial class HighEntropyPathRedactor : IRedactor
{
    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var stage1 = Base64UrlSegment().Replace(input, "/<redacted>");
        return HexSegment().Replace(stage1, "/<redacted>");
    }

    // The leading negative lookahead bails out when the segment is UUID-shaped
    // (8-4-4-4-12 hex with dashes at fixed positions). UUIDs are routine resource
    // IDs in this codebase; redacting them would mangle most paths in the logs.
    [GeneratedRegex(@"/(?![0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?:[/?\s""]|$))[A-Za-z0-9_-]{24,}(?=[/?\s""]|$)")]
    private static partial Regex Base64UrlSegment();

    [GeneratedRegex(@"/[a-fA-F0-9]{16,}(?=[/?\s""]|$)")]
    private static partial Regex HexSegment();
}
