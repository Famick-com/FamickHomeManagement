namespace Famick.HomeManagement.Core.Configuration;

/// <summary>
/// Guards the file names that get joined onto a storage path.
/// </summary>
/// <remarks>
/// <para>
/// These names come from the database, and until restore existed that made them trustworthy —
/// every one was generated on upload. Restore changed that: an archive is a file somebody hands
/// you, and the rows in it become those columns verbatim. A <c>FileName</c> of
/// <c>"../../../../etc/passwd"</c> survives <see cref="Path.Combine(string,string)"/> untouched,
/// walks out of the storage root, and is then served through the download endpoint the owner is
/// perfectly entitled to call.
/// </para>
/// <para>
/// Enforced here, at the point of use, rather than only on the way in. Validating the archive
/// stops the bad row being written; validating here means it does not matter how a bad row got
/// there — an older archive, a manual edit, a future import path nobody has written yet.
/// </para>
/// </remarks>
public static class StoredFileName
{
    /// <summary>
    /// Whether <paramref name="fileName"/> is a plain file name, safe to join onto a directory.
    /// </summary>
    public static bool IsSafe(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        // Anything that navigates. Both separators are checked whatever the host platform,
        // because the value may have been written by a different one.
        if (fileName.Contains('/') || fileName.Contains('\\')) return false;
        if (fileName.Contains("..", StringComparison.Ordinal)) return false;

        // A drive or scheme prefix — "C:evil" resolves relative to that drive's directory.
        if (fileName.Contains(':')) return false;

        // Control characters, and anything the platform rejects outright.
        if (fileName.Any(char.IsControl)) return false;
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;

        // "." and ".." are directories, not files.
        return fileName.Trim('.').Length != 0;
    }

    /// <summary>
    /// Returns <paramref name="fileName"/> when it is safe to use, and throws when it is not.
    /// </summary>
    /// <remarks>
    /// Throws rather than sanitising. A name that fails this is not a slightly wrong name to be
    /// tidied up — it is a row that should never have been written, and quietly repairing it would
    /// hide that from whoever has to work out where it came from.
    /// </remarks>
    public static string Require(string? fileName, string context)
    {
        if (IsSafe(fileName)) return fileName!;

        throw new InvalidOperationException(
            $"Refusing to use '{fileName}' as a {context} file name: it is not a plain file name. " +
            "A stored name containing path separators points at data written from outside the " +
            "application rather than by an upload.");
    }
}
