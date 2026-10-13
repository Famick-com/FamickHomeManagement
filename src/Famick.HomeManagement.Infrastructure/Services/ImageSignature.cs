namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Identifies a raster image by its leading bytes. Fetched images are stored under
/// the type found here, never the one a remote server claimed, so an SVG or HTML
/// document labelled <c>image/png</c> is refused instead of being served back from
/// the app's origin (issue #108).
/// </summary>
public static class ImageSignature
{
    public sealed record Format(string ContentType, string Extension);

    public static readonly Format Jpeg = new("image/jpeg", ".jpg");
    public static readonly Format Png = new("image/png", ".png");
    public static readonly Format Gif = new("image/gif", ".gif");
    public static readonly Format Webp = new("image/webp", ".webp");

    /// <summary>Returns the format, or null when the bytes are not JPEG, PNG, GIF or WebP.</summary>
    public static Format? Detect(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            return Jpeg;
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
            return Png;
        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
            return Gif;
        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
            return Webp;
        return null;
    }
}
