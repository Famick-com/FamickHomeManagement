using System.Net.Http.Headers;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Service for caching product images locally.
/// Uses a dedicated HttpClient that does NOT go through DynamicApiHttpHandler,
/// so external image URLs (e.g., OpenFoodFacts) are fetched directly.
/// </summary>
public class ImageCacheService
{
    private readonly HttpClient _imageHttpClient;
    private readonly TokenStorage _tokenStorage;
    private readonly ApiSettings _apiSettings;
    private readonly string _cacheDirectory;

    public ImageCacheService(TokenStorage tokenStorage, ApiSettings apiSettings)
    {
        _tokenStorage = tokenStorage;
        _apiSettings = apiSettings;

        // Dedicated HttpClient without DynamicApiHttpHandler so external URLs work
#if DEBUG
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _imageHttpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
#else
        _imageHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
#endif

        _cacheDirectory = Path.Combine(FileSystem.CacheDirectory, "images");

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <summary>
    /// Caches all product images for a shopping session using the original API item data.
    /// </summary>
    public async Task CacheImagesForSessionAsync(ShoppingSession session, List<ShoppingListItemDto>? apiItems = null)
    {
        if (apiItems == null || apiItems.Count == 0) return;

        // Build a lookup from item ID to image URL
        var imageUrls = apiItems
            .Where(i => !string.IsNullOrEmpty(i.ImageUrl))
            .ToDictionary(i => i.Id, i => i.ImageUrl!);

        System.Diagnostics.Debug.WriteLine($"[ImageCache] {apiItems.Count} API items, {imageUrls.Count} have ImageUrl");

        var tasks = new List<Task>();

        foreach (var item in session.Items)
        {
            if (!string.IsNullOrEmpty(item.LocalImagePath))
                continue;

            if (imageUrls.TryGetValue(item.Id, out var imageUrl))
            {
                var capturedItem = item;
                var capturedUrl = imageUrl;
                tasks.Add(Task.Run(async () =>
                {
                    var localPath = await CacheImageAsync(capturedUrl);
                    if (localPath != null)
                    {
                        capturedItem.LocalImagePath = localPath;
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Downloads and caches an image, returning the local path.
    /// </summary>
    public async Task<string?> CacheImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        try
        {
            // Generate a unique filename based on URL hash
            var hash = imageUrl.GetHashCode().ToString("X");
            var extension = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
            if (string.IsNullOrEmpty(extension)) extension = ".jpg";

            var localPath = Path.Combine(_cacheDirectory, $"{hash}{extension}");

            // Check if already cached
            if (File.Exists(localPath))
            {
                return localPath;
            }

            System.Diagnostics.Debug.WriteLine($"[ImageCache] Downloading: {imageUrl}");

            // Download and save
            var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);

            // Add auth header for API-hosted images
            var apiBase = _apiSettings.BaseUrl.TrimEnd('/');
            if (imageUrl.StartsWith(apiBase, StringComparison.OrdinalIgnoreCase))
            {
                var token = await _tokenStorage.GetAccessTokenAsync();
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _imageHttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var imageData = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(localPath, imageData);

            return localPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageCache] Failed to cache {imageUrl}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the local path for a cached image if it exists.
    /// </summary>
    public string? GetCachedImagePath(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        try
        {
            var hash = imageUrl.GetHashCode().ToString("X");
            var extension = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
            if (string.IsNullOrEmpty(extension)) extension = ".jpg";

            var localPath = Path.Combine(_cacheDirectory, $"{hash}{extension}");

            return File.Exists(localPath) ? localPath : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clears all cached images.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, recursive: true);
                Directory.CreateDirectory(_cacheDirectory);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Gets the total size of cached images in bytes.
    /// </summary>
    public long GetCacheSizeBytes()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                return 0;

            return new DirectoryInfo(_cacheDirectory)
                .GetFiles()
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }
}
