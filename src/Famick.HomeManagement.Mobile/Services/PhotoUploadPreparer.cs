using Microsoft.Maui.Graphics.Platform;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Normalises a picked or captured photo into something the server's photo endpoints
/// will accept.
///
/// This exists because of FHM-50: iOS's <c>MediaPicker.CapturePhotoAsync</c> returns a
/// full-resolution **PNG**, and a single shot from an iPhone 13 Pro Max measured
/// 20,502,339 bytes — roughly double the server's 10 MB cap. Gallery picks are ordinary
/// JPEGs an order of magnitude smaller, which is why only the camera path failed.
/// Rather than reject the photo, downscale and re-encode it as JPEG so it fits.
/// </summary>
public static class PhotoUploadPreparer
{
    /// <summary>Mirrors <c>StorageBinsController.MaxPhotoSize</c>.</summary>
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Above this, re-encode even when the type is already acceptable — a 4 MB+ photo is
    /// far more resolution than any of these views display, and shrinking it is the
    /// difference between an upload that completes on cellular and one that doesn't.
    /// </summary>
    private const long RecompressThresholdBytes = 4 * 1024 * 1024;

    /// <summary>Longest edge after downscaling. Comfortably above any display size in the app.</summary>
    private const float MaxDimension = 2048f;

    private const float JpegQuality = 0.85f;

    /// <summary>
    /// The image MIME types the server's photo endpoints accept. A part sent with anything
    /// else comes back as a 400 "File type not allowed". Mirrors
    /// <c>StorageBinsController.AllowedPhotoTypes</c>; membership is case-insensitive.
    /// </summary>
    public static readonly IReadOnlySet<string> ServerAcceptedMimes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/gif", "image/webp"
        };

    /// <summary>A photo ready to hand to an upload call.</summary>
    /// <param name="Stream">Seekable, positioned at 0. The caller owns it.</param>
    public sealed record PreparedPhoto(Stream Stream, string FileName, string ContentType, bool WasReEncoded);

    /// <summary>
    /// Returns the photo unchanged when it is already small enough and of an accepted type,
    /// and otherwise a downscaled JPEG copy. Never throws for image-decoding reasons — if
    /// re-encoding fails, the original is returned so the caller's normal error handling
    /// reports the real server response instead of a decode error.
    /// </summary>
    public static async Task<PreparedPhoto> PrepareAsync(Stream source, string fileName, string? contentType)
    {
        var mime = contentType ?? string.Empty;
        var isAcceptedType = ServerAcceptedMimes.Contains(mime);

        long? length = source.CanSeek ? source.Length : null;
        var needsWork = !isAcceptedType
            || length is null
            || length > RecompressThresholdBytes;

        if (!needsWork)
        {
            if (source.CanSeek) source.Position = 0;
            return new PreparedPhoto(source, fileName, mime, WasReEncoded: false);
        }

        try
        {
            if (source.CanSeek) source.Position = 0;

            using var original = PlatformImage.FromStream(source);
            using var resized = original.Downsize(MaxDimension, disposeOriginal: false);

            var buffer = new MemoryStream();
            await resized.SaveAsync(buffer, ImageFormat.Jpeg, JpegQuality).ConfigureAwait(false);
            buffer.Position = 0;

            var jpegName = Path.ChangeExtension(
                string.IsNullOrWhiteSpace(fileName) ? "photo" : fileName, ".jpg");

            Console.WriteLine(
                $"[PhotoUploadPreparer] re-encoded '{fileName}' ({mime}, {length?.ToString() ?? "?"} bytes) " +
                $"-> '{jpegName}' (image/jpeg, {buffer.Length} bytes)");

            await source.DisposeAsync().ConfigureAwait(false);
            return new PreparedPhoto(buffer, jpegName, "image/jpeg", WasReEncoded: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PhotoUploadPreparer] re-encode failed ({ex.GetType().Name}: {ex.Message}); sending original");
            if (source.CanSeek) source.Position = 0;
            return new PreparedPhoto(source, fileName, mime, WasReEncoded: false);
        }
    }
}
