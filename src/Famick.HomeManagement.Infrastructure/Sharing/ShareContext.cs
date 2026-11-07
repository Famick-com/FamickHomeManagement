using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Services;

namespace Famick.HomeManagement.Infrastructure.Sharing;

/// <summary>How much one shared item may carry into another household.</summary>
public static class ShareLimits
{
    public const long MaxImageBytes = 10 * 1024 * 1024;
    public const long MaxTotalImageBytes = 50 * 1024 * 1024;
    public const int MaxImages = 30;
    public const int MaxSteps = 100;
    public const int MaxIngredientLines = 500;
    public const int MaxNestedRecipes = 25;
    public const int MaxNestingDepth = 5;
    public const int MaxMealItems = 100;
    public const int MaxEntities = 2000;
}

/// <summary>
/// Where a copy came from: the household that shared it and the household receiving it. An item's
/// origin key is stable for one recipient, so the same item added twice is found, and differs between
/// recipients, so two households' copies can't be tied together.
/// </summary>
public sealed class ShareOrigin(Guid ownerTenantId, Guid recipientTenantId, string sharedByHouseholdName)
{
    public string SharedByHouseholdName { get; } = sharedByHouseholdName;

    public string KeyFor(ShareableKind kind, Guid sourceId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{recipientTenantId:N}|{ownerTenantId:N}|{(int)kind}|{sourceId:N}"));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>A picture read from the sharing household, checked to be a JPEG, PNG or WebP image.</summary>
public sealed record SharedImageBytes(byte[] Bytes, string ContentType, string Extension);

/// <summary>
/// Collects what a snapshot reads in the sharing household: the pictures, read while that household's
/// storage is in reach, and a count of everything, so an item too large to copy stops early.
/// </summary>
public sealed class ShareSnapshotContext(int maxImages = ShareLimits.MaxImages)
{
    private readonly Dictionary<string, SharedImageBytes> _images = new();
    private long _imageBytes;
    private int _entities, _steps, _lines, _nested, _mealItems;

    public IReadOnlyDictionary<string, SharedImageBytes> Images => _images;

    public int SkippedImages { get; private set; }

    /// <summary>Counts rows the copy will write; every other count adds to this one too.</summary>
    public void CountEntities(int count = 1) => Check(ref _entities, count, ShareLimits.MaxEntities, "items");

    public void CountSteps(int count) => Count(ref _steps, count, ShareLimits.MaxSteps, "steps");
    public void CountIngredientLines(int count) => Count(ref _lines, count, ShareLimits.MaxIngredientLines, "ingredient lines");
    public void CountMealItems(int count) => Count(ref _mealItems, count, ShareLimits.MaxMealItems, "meal items");

    public void CountNestedRecipe(int depth)
    {
        if (depth > ShareLimits.MaxNestingDepth)
            throw new ShareTooLargeException("This recipe nests other recipes too deeply to share");
        Count(ref _nested, 1, ShareLimits.MaxNestedRecipes, "nested recipes");
    }

    private void Count(ref int counter, int count, int max, string what)
    {
        Check(ref counter, count, max, what);
        CountEntities(count);
    }

    private static void Check(ref int counter, int count, int max, string what)
    {
        counter += count;
        if (counter > max)
            throw new ShareTooLargeException($"This has too many {what} to share");
    }

    /// <summary>
    /// Reads a picture and returns the key the copy finds it by, or null when it is left out: missing,
    /// over the size limits, past the number a copy carries, or not a JPEG, PNG or WebP image. The bytes
    /// decide the format, never a stored file name or content type.
    /// </summary>
    public async Task<string?> AddImageAsync(Func<CancellationToken, Task<Stream?>> open, CancellationToken ct)
    {
        if (_images.Count >= maxImages)
        {
            // A preview reads one picture by design; only a copy counts what it leaves out.
            if (maxImages == ShareLimits.MaxImages)
                SkippedImages++;
            return null;
        }

        byte[]? bytes;
        await using (var stream = await open(ct))
            bytes = stream == null ? null : await ReadCappedAsync(stream, ShareLimits.MaxImageBytes, ct);

        var format = bytes == null ? null : ImageSignature.Detect(bytes);
        if (bytes == null || format == null || format.ContentType == "image/gif"
            || _imageBytes + bytes.Length > ShareLimits.MaxTotalImageBytes)
        {
            SkippedImages++;
            return null;
        }

        _imageBytes += bytes.Length;
        var key = $"image-{_images.Count}";
        _images[key] = new SharedImageBytes(bytes, format.ContentType, format.Extension);
        return key;
    }

    private static async Task<byte[]?> ReadCappedAsync(Stream stream, long max, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > max)
                return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}

/// <summary>
/// What an import writes with in the receiving household: the pictures read from the sharing
/// household, the origin, and the files saved so far, which are removed if the copy fails.
/// </summary>
public sealed class ShareImportContext(ShareSnapshotContext snapshot, ShareOrigin origin)
{
    private readonly List<Func<Task>> _undo = new();

    public ShareOrigin Origin { get; } = origin;

    public SharedImageBytes? Image(string? key) =>
        key != null && snapshot.Images.TryGetValue(key, out var image) ? image : null;

    /// <summary>Records how to remove a file this copy saved, should the copy not go through.</summary>
    public void SavedFile(Func<Task> remove) => _undo.Add(remove);

    internal IReadOnlyList<Func<Task>> SavedFiles => _undo;
}
