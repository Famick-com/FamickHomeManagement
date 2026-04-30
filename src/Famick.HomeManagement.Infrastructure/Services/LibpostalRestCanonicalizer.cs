using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// <see cref="IAddressCanonicalizer"/> backed by a libpostal-rest sidecar
/// (e.g. <c>johnlonganecker/libpostal-rest</c>). Calls <c>/expand</c> to
/// get a canonical form, then <c>/parser</c> to split it back into
/// labeled components for per-component hashing. Results are cached for
/// 24 hours sliding so repeat hashes don't hit the sidecar repeatedly.
///
/// All failure modes (HTTP error, timeout, deserialization, missing
/// labels) degrade gracefully to the input components — writes never
/// break because libpostal is down.
/// </summary>
public sealed class LibpostalRestCanonicalizer : IAddressCanonicalizer
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly LibpostalOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LibpostalRestCanonicalizer> _logger;

    public LibpostalRestCanonicalizer(
        HttpClient httpClient,
        IOptions<LibpostalOptions> options,
        IMemoryCache cache,
        ILogger<LibpostalRestCanonicalizer> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }
    }

    public string ProviderName => "Libpostal";

    public async Task<CanonicalAddressComponents> CanonicalizeAsync(
        AddressComponentsInput input,
        CancellationToken ct = default)
    {
        var assembled = AssembleQuery(input);
        if (string.IsNullOrWhiteSpace(assembled))
        {
            return Passthrough(input);
        }

        var cacheKey = $"libpostal:{assembled}";
        if (_cache.TryGetValue(cacheKey, out CanonicalAddressComponents? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var expansion = await ExpandFirstAsync(assembled, ct);
            if (string.IsNullOrWhiteSpace(expansion))
            {
                return CacheAndReturn(cacheKey, Passthrough(input));
            }

            var parsed = await ParseAsync(expansion, ct);
            if (parsed.Count == 0)
            {
                return CacheAndReturn(cacheKey, Passthrough(input));
            }

            var canonical = MapParsedToComponents(parsed, input);
            return CacheAndReturn(cacheKey, canonical);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Real user cancellation — propagate.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libpostal canonicalization failed for '{Query}'; falling back to input components.", assembled);
            return Passthrough(input);
        }
    }

    private static string AssembleQuery(AddressComponentsInput input)
    {
        var parts = new[] { input.Line1, input.City, input.State, input.PostalCode, input.Country }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim());
        return string.Join(", ", parts);
    }

    private static CanonicalAddressComponents Passthrough(AddressComponentsInput input) =>
        new(input.Line1?.Trim(), input.City?.Trim(), input.State?.Trim(),
            input.PostalCode?.Trim(), input.Country?.Trim());

    private CanonicalAddressComponents CacheAndReturn(string key, CanonicalAddressComponents value)
    {
        _cache.Set(key, value, new MemoryCacheEntryOptions { SlidingExpiration = CacheTtl });
        return value;
    }

    private async Task<string?> ExpandFirstAsync(string query, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("expand", new QueryRequest(query), ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("libpostal /expand returned {StatusCode}", response.StatusCode);
            return null;
        }

        var expansions = await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions, ct);
        return expansions?.FirstOrDefault();
    }

    private async Task<List<ParsedComponent>> ParseAsync(string query, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("parser", new QueryRequest(query), ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("libpostal /parser returned {StatusCode}", response.StatusCode);
            return new();
        }

        return await response.Content.ReadFromJsonAsync<List<ParsedComponent>>(JsonOptions, ct)
            ?? new();
    }

    private static CanonicalAddressComponents MapParsedToComponents(
        IReadOnlyList<ParsedComponent> parsed,
        AddressComponentsInput original)
    {
        // libpostal labels: house_number, road, unit, level, staircase,
        // entrance, po_box, postcode, suburb, city_district, city, island,
        // state_district, state, country_region, country, world_region.
        // We reconstruct Line1 from house_number + road (intentionally
        // excluding unit — that's per-contact and lives on ContactAddress,
        // not on the shared Address row's hash inputs).
        string? Find(string label) => parsed.FirstOrDefault(p =>
            string.Equals(p.Label, label, StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

        var houseNumber = Find("house_number");
        var road = Find("road");
        var line1 = string.Join(" ",
            new[] { houseNumber, road }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        return new CanonicalAddressComponents(
            string.IsNullOrWhiteSpace(line1) ? original.Line1?.Trim() : line1,
            Find("city") ?? original.City?.Trim(),
            Find("state") ?? original.State?.Trim(),
            Find("postcode") ?? original.PostalCode?.Trim(),
            Find("country") ?? original.Country?.Trim());
    }

    private sealed record QueryRequest(
        [property: JsonPropertyName("query")] string Query);

    private sealed record ParsedComponent(
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("value")] string? Value);
}

public sealed class LibpostalOptions
{
    public const string SectionName = "Libpostal";

    /// <summary>Base URL of the libpostal-rest sidecar (e.g. <c>http://libpostal:8080</c>).</summary>
    public string BaseUrl { get; set; } = "http://libpostal:8080";

    /// <summary>Per-request HTTP timeout. Defaults to 5 seconds — libpostal is fast.</summary>
    public int TimeoutSeconds { get; set; } = 5;
}
