using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Web.Shared.Http;

/// <summary>
/// Header values endpoints set for themselves, ahead of <see cref="Middleware.SecurityHeadersMiddleware"/>.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// For files people uploaded. If one is opened directly it runs as an opaque origin with no
    /// scripts, so an HTML or SVG file cannot act as the app.
    /// </summary>
    public const string SandboxPolicy = "sandbox";

    private const string PdfContentType = "application/pdf";

    /// <summary>How far into a file a PDF reader looks for the <c>%PDF-</c> header.</summary>
    private const int PdfHeaderWindow = 1024;

    /// <summary>
    /// Marks a stored file as sandboxed. PDFs are left out: Chrome will not render a PDF under a
    /// sandbox policy, and its viewer does not run the document's scripts as the app anyway.
    /// </summary>
    /// <remarks>
    /// The exemption goes by the stored content type, which the uploader declared. Uploads that
    /// declare a PDF must pass <see cref="IsPdf"/>, so the label cannot be put on other bytes. Even
    /// then the response is still served as <c>application/pdf</c> with <c>nosniff</c>, so the
    /// browser never renders it as HTML.
    /// </remarks>
    public static void SandboxStoredFile(HttpResponse response, string? contentType)
    {
        if (!IsPdfContentType(contentType))
            response.Headers.ContentSecurityPolicy = SandboxPolicy;
    }

    public static bool IsPdfContentType(string? contentType) =>
        string.Equals(contentType, PdfContentType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="stream"/> holds a PDF: <c>%PDF-</c> within the first kilobyte, the
    /// same window PDF readers allow. The stream must be seekable; it is rewound afterwards.
    /// </summary>
    public static async Task<bool> IsPdf(Stream stream, CancellationToken cancellationToken = default)
    {
        var start = stream.Position;
        var buffer = new byte[PdfHeaderWindow];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        stream.Position = start;

        return buffer.AsSpan(0, read).IndexOf("%PDF-"u8) >= 0;
    }
}
