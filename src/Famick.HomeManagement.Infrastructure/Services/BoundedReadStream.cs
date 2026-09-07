namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Caps how many bytes may be read from an underlying stream.
/// </summary>
/// <remarks>
/// Used to serve a byte range from a seekable file: seek to the start, then stop at the end.
/// Read-only and forward-only on purpose — it exists to bound a response body, not to be a
/// general-purpose stream.
/// </remarks>
internal sealed class BoundedReadStream(Stream inner, long limit) : Stream
{
    private long _remaining = limit;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => limit;

    public override long Position
    {
        get => limit - _remaining;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_remaining <= 0) return 0;

        var read = inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
        _remaining -= read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_remaining <= 0) return 0;

        var slice = buffer[..(int)Math.Min(buffer.Length, _remaining)];
        var read = await inner.ReadAsync(slice, ct);
        _remaining -= read;
        return read;
    }

    public override void Flush() => inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
