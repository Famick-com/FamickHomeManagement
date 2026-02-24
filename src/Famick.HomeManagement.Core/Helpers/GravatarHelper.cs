using System.Security.Cryptography;
using System.Text;

namespace Famick.HomeManagement.Core.Helpers;

/// <summary>
/// Helper class for generating Gravatar image URLs
/// </summary>
public static class GravatarHelper
{
    /// <summary>
    /// Generates a Gravatar URL for the given email address
    /// </summary>
    /// <param name="email">The email address to generate a Gravatar URL for</param>
    /// <param name="size">The desired image size in pixels (default 200)</param>
    /// <param name="defaultImage">The default image to use if no Gravatar exists (default "404" to return 404 if not found)</param>
    /// <returns>The Gravatar URL, or null if email is empty</returns>
    public static string? GetGravatarUrl(string? email, int size = 200, string defaultImage = "404")
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(normalizedEmail));
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();

        return $"https://www.gravatar.com/avatar/{hashString}?s={size}&d={defaultImage}";
    }
}
