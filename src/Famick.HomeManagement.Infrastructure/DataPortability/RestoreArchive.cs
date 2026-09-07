namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// An uploaded archive held somewhere it can be read from repeatedly, and independently.
/// </summary>
/// <remarks>
/// <para>
/// A restore reads the same archive many times over: once to inspect it, once per table to
/// classify, once per table to apply, and once per attachment. An object-storage stream cannot be
/// rewound, so the bytes have to land somewhere seekable first.
/// </para>
/// <para>
/// The important part is <em>independently</em>. Sharing one stream and seeking it looks
/// equivalent and is not: reading a table is a suspended enumeration holding an open zip entry,
/// and moving the position underneath it to fetch an attachment leaves that enumeration reading
/// from somewhere it did not expect. Each caller gets its own handle instead.
/// </para>
/// </remarks>
internal sealed class RestoreArchive : IAsyncDisposable
{
    private readonly string _path;

    private RestoreArchive(string path) => _path = path;

    /// <summary>
    /// Copies an upload somewhere it can be read from more than once.
    /// </summary>
    public static async Task<RestoreArchive> BufferAsync(Stream source, CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), $"famick-restore-{Guid.NewGuid():N}.zip");

        await using (var file = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81920, FileOptions.Asynchronous))
        {
            await source.CopyToAsync(file, ct);
        }

        await source.DisposeAsync();
        return new RestoreArchive(path);
    }

    /// <summary>
    /// A fresh handle on the archive, positioned at the start and owned by the caller.
    /// </summary>
    public Stream OpenRead() => new FileStream(
        _path, FileMode.Open, FileAccess.Read, FileShare.Read,
        bufferSize: 81920, FileOptions.Asynchronous);

    public ValueTask DisposeAsync()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch (IOException)
        {
            // A temp file that outlives the restore is untidy, not harmful, and the operating
            // system clears the directory eventually. Not worth failing a completed restore over.
        }

        return ValueTask.CompletedTask;
    }
}
