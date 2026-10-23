using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// Serving a stored archive, and the rules for a link to one that leaves by email.
/// </summary>
/// <remarks>
/// Shared by the household export and a member's own, which store their archives the same way
/// and differ only in who may ask for one.
/// </remarks>
internal static class ArchiveDownloads
{
    /// <summary>
    /// Opens a finished archive for download, honouring a byte range.
    /// </summary>
    /// <remarks>
    /// Takes the transfer already looked up, because who may look it up is the caller's
    /// business: any member for a household export, a token naming it for a member's own.
    /// </remarks>
    public static async Task<ExportDownloadResult> OpenAsync(
        IFileStorageService storage, HouseholdDataTransfer? transfer,
        long? rangeStart, long? rangeEnd, CancellationToken ct)
    {
        if (transfer?.ArchiveFileName == null) return ExportDownloadResult.Unavailable;
        if (transfer.Status != HouseholdDataTransferStatus.Completed) return ExportDownloadResult.Unavailable;

        // Expiry is answered from the record, not from whether the object happens to still be
        // there. Otherwise the answer depends on which side deleted first.
        if (transfer.ExpiresAt is null || transfer.ExpiresAt <= DateTime.UtcNow) return ExportDownloadResult.Unavailable;

        var info = await storage.GetExportArchiveInfoAsync(transfer.Id, transfer.ArchiveFileName, ct);
        if (info == null) return ExportDownloadResult.Unavailable;

        var range = ResolveRange(rangeStart, rangeEnd, info.Length);
        if (range is null) return ExportDownloadResult.RangeNotSatisfiable;

        var (start, end) = range.Value;

        var stream = await storage.GetExportArchiveStreamAsync(
            transfer.Id, transfer.ArchiveFileName, start, end, ct);

        return stream == null
            ? ExportDownloadResult.Unavailable
            : ExportDownloadResult.Ok(new ExportDownload(stream, transfer.ArchiveFileName, info.Length, start, end));
    }

    /// <summary>
    /// Turns whatever the client asked for into concrete bounds, or null when it cannot be served.
    /// </summary>
    /// <remarks>
    /// Three forms, and only the first is obvious. <c>bytes=100-200</c> is explicit;
    /// <c>bytes=100-</c> runs to the end; and <c>bytes=-500</c> is a suffix asking for the last
    /// 500 bytes, which arrives with no start at all. Left unresolved, that last form reads as
    /// "no range" and quietly returns the whole archive with a 200.
    /// </remarks>
    internal static (long Start, long End)? ResolveRange(long? rangeStart, long? rangeEnd, long totalLength)
    {
        if (rangeStart is null && rangeEnd is null) return (0, totalLength - 1);
        if (totalLength == 0) return null;

        if (rangeStart is null)
        {
            // Suffix: the last N bytes, clamped to the whole archive when N exceeds it.
            var suffixLength = rangeEnd!.Value;
            if (suffixLength <= 0) return null;

            var suffixStart = Math.Max(0, totalLength - suffixLength);
            return (suffixStart, totalLength - 1);
        }

        // A start at or past the end is unsatisfiable rather than empty — answering it with zero
        // bytes and a 206 tells the client the file ended where it did not.
        if (rangeStart.Value < 0 || rangeStart.Value >= totalLength) return null;

        var resolvedEnd = Math.Min(rangeEnd ?? totalLength - 1, totalLength - 1);
        return resolvedEnd < rangeStart.Value ? null : (rangeStart.Value, resolvedEnd);
    }

    /// <summary>
    /// Whether a download link can go out by email.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other URL the storage service builds is handed to a browser that already knows
    /// what host it is on, so a relative one works there and nobody notices the base URL is
    /// unset. An emailed link is the only one that leaves the building, and an email client has
    /// nothing to resolve it against — the recipient gets a link that cannot work. Refusing to
    /// send beats sending a dead link: a broken email is worse than none because it looks like
    /// the feature failed.
    /// </para>
    /// <para>
    /// UriKind.Absolute alone is not the test. On Unix a leading slash parses happily as an
    /// absolute file:// URI, so "/api/v1/..." would pass and the check would do nothing on the
    /// very platform this runs on. It has to be a web address: http or https, with a host.
    /// </para>
    /// <para>
    /// The link carries an archive token in its query string, worth the archive's contents for
    /// a week. Sending it over cleartext to a public host puts it in front of anyone on the
    /// path. Not an unconditional HTTPS requirement, though. Self-hosted on a home network is a
    /// supported deployment — .env.example suggests http://192.168.1.50:8088 — and there the
    /// whole application is already served over the same cleartext link, session token
    /// included. Demanding TLS for this one link would withhold the email from installs that
    /// accept that trade everywhere else, without making them any safer.
    /// </para>
    /// </remarks>
    public static EmailedLinkProblem CheckEmailedLink(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(parsed.Host))
        {
            return EmailedLinkProblem.NotAbsolute;
        }

        return parsed.Scheme != Uri.UriSchemeHttps && !IsPrivateNetworkHost(parsed)
            ? EmailedLinkProblem.Cleartext
            : EmailedLinkProblem.None;
    }

    /// <summary>Scheme and authority only, for a log line that must not carry the token.</summary>
    public static string Origin(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var parsed) ? $"{parsed.Scheme}://{parsed.Authority}" : "(none)";

    /// <summary>
    /// Strips anything secret from a link before it is logged: the token in the query string, and
    /// any credentials embedded in the authority.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called on a link that failed to parse as a web address, so it is done textually rather than
    /// through <see cref="Uri"/> — reaching for that type to clean up something just established
    /// not to be one would be the wrong tool.
    /// </para>
    /// <para>
    /// The credentials case is not hypothetical here. A base URL of
    /// <c>ftp://user:pass@host/</c> fails the scheme check and lands in this log line, so
    /// stripping only the query would still write the password out.
    /// </para>
    /// </remarks>
    public static string Redact(string link)
    {
        var query = link.IndexOf('?', StringComparison.Ordinal);
        var trimmed = query < 0 ? link : string.Concat(link.AsSpan(0, query), "?<redacted>");

        var separator = trimmed.IndexOf("//", StringComparison.Ordinal);
        if (separator < 0) return trimmed;

        var authorityStart = separator + 2;
        var authorityEnd = trimmed.IndexOf('/', authorityStart);
        if (authorityEnd < 0) authorityEnd = trimmed.Length;

        var credentials = trimmed.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart);
        return credentials < 0
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, authorityStart), "<redacted>@", trimmed.AsSpan(credentials + 1));
    }

    /// <summary>
    /// Whether the host is one only reachable from inside a private network.
    /// </summary>
    /// <remarks>
    /// Used to decide whether cleartext is acceptable for a link carrying a token. Deliberately
    /// generous about what counts as private — a self-hosted server may be addressed by bare
    /// hostname, mDNS name, or private IP — because the cost of getting it wrong in that
    /// direction is a withheld email, while the alternative is refusing to serve a supported
    /// deployment.
    /// </remarks>
    public static bool IsPrivateNetworkHost(Uri uri)
    {
        if (uri.IsLoopback) return true;

        if (System.Net.IPAddress.TryParse(uri.Host, out var address))
        {
            var octets = address.GetAddressBytes();

            return address.AddressFamily switch
            {
                // RFC 1918, plus link-local.
                System.Net.Sockets.AddressFamily.InterNetwork =>
                    octets[0] == 10
                    || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
                    || (octets[0] == 192 && octets[1] == 168)
                    || (octets[0] == 169 && octets[1] == 254),

                // Unique local and link-local.
                System.Net.Sockets.AddressFamily.InterNetworkV6 =>
                    address.IsIPv6LinkLocal || (octets[0] & 0xFE) == 0xFC,

                _ => false,
            };
        }

        // A single-label name — "homeserver" — cannot be resolved from the public internet.
        if (!uri.Host.Contains('.')) return true;

        return uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".home.arpa", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Why a download link cannot be emailed, if it cannot.</summary>
internal enum EmailedLinkProblem
{
    None,

    /// <summary>Not a web address with a host — BaseUrl is unset or wrong.</summary>
    NotAbsolute,

    /// <summary>Plain http to a host reachable from the public internet.</summary>
    Cleartext,
}
