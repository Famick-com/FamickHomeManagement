namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// What a storage backend can say about a stored file without opening it.
/// </summary>
/// <param name="Length">Size in bytes.</param>
/// <param name="LastModifiedUtc">When it was written.</param>
public sealed record StoredFileInfo(long Length, DateTime LastModifiedUtc);
