using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Famick.HomeManagement.Web.Shared.Http;

/// <summary>
/// Cache validators for the file-download endpoints.
///
/// These endpoints used to return a bare body with no Cache-Control, ETag or
/// Last-Modified, so a client had nothing to revalidate against and refetched
/// every file on every render. A page of 230 product images was re-requesting
/// all of them roughly 48 times; each fetch costs a DB lookup, and the parallel
/// bursts inflated the Npgsql pool to its 100-connection ceiling (issue #80).
/// </summary>
public static class StoredFileCache
{
    /// <summary>
    /// How long a stored file may be held in a client cache. Stored files are
    /// write-once — replacing an image adds a row with a new id, so the download
    /// URL changes — which is what makes a long lifetime safe here.
    /// </summary>
    public const int MaxAgeSeconds = 31_536_000; // one year

    /// <summary>
    /// Marks the response cacheable by this client alone and returns the ETag to
    /// hand to <c>File(...)</c>, which uses it to answer If-None-Match with a 304
    /// once max-age lapses rather than restreaming the body from storage.
    /// </summary>
    /// <param name="response">The response to annotate</param>
    /// <param name="versionKey">
    /// Stable identifier for this exact content, which must change whenever the
    /// bytes do. The stored unique filename is the natural choice —
    /// <c>GenerateUniqueFileName</c> makes a fresh one per upload.
    /// </param>
    public static EntityTagHeaderValue Apply(HttpResponse response, string versionKey)
    {
        // private: these are tenant-scoped and must never land in a shared cache.
        response.Headers.CacheControl = $"private, max-age={MaxAgeSeconds}, immutable";
        return ComputeETag(versionKey);
    }

    /// <summary>
    /// Derives a stable, quoted ETag from a version key.
    /// </summary>
    public static EntityTagHeaderValue ComputeETag(string versionKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(versionKey));
        return new EntityTagHeaderValue($"\"{Convert.ToHexString(hash).ToLowerInvariant()[..32]}\"");
    }
}
