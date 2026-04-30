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
/// (<c>rezaq/libpostal-rest</c>). Calls the <c>/expandparser</c> endpoint
/// once per address — that returns every expansion plus its parse, so we
/// can pick the structurally-correct expansion in a single round-trip.
/// Results are cached for 24 hours sliding so repeat hashes don't re-hit
/// the sidecar.
///
/// The reason we don't just take the first <c>/expand</c> result: libpostal
/// expands ambiguous tokens like <c>St</c> to BOTH <c>street</c> and <c>saint</c>,
/// and the order isn't guaranteed. Picking blindly produces unstable
/// hashes. <c>/expandparser</c> lets us discard expansions whose parse
/// drifts (e.g. <c>St</c> → <c>saint</c> reassigns tokens to the city) and
/// pick the longest road — that empirically picks the correct expansion.
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
            var entries = await ExpandParseAsync(assembled, ct);
            if (entries.Count == 0)
            {
                return CacheAndReturn(cacheKey, Passthrough(input));
            }

            var best = PickBest(entries);
            if (best is null)
            {
                return CacheAndReturn(cacheKey, Passthrough(input));
            }

            var canonical = MapParsedToComponents(best.Parsed ?? new(), input);
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

    private async Task<List<ExpandParseEntry>> ExpandParseAsync(string query, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("expandparser", new QueryRequest(query), ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("libpostal /expandparser returned {StatusCode}", response.StatusCode);
            return new();
        }

        return await response.Content.ReadFromJsonAsync<List<ExpandParseEntry>>(JsonOptions, ct)
            ?? new();
    }

    /// <summary>
    /// Picks the structurally-correct expansion. Filters to the
    /// <c>type=expansion</c> entries and returns the one whose parsed
    /// <c>road</c> value is longest — empirically that picks the right
    /// expansion when libpostal offers both <c>St</c>→<c>street</c> and
    /// <c>St</c>→<c>saint</c> variants, because the bad expansion
    /// reassigns the suffix away from the road component (shortening it).
    /// Falls back to the original query parse if no expansion qualifies.
    /// Deterministic tie-break: alphabetical first.
    /// </summary>
    private static ExpandParseEntry? PickBest(IReadOnlyList<ExpandParseEntry> entries)
    {
        var expansions = entries.Where(e =>
            string.Equals(e.Type, "expansion", StringComparison.OrdinalIgnoreCase)
            && e.Parsed is { Count: > 0 }).ToList();

        if (expansions.Count == 0)
        {
            // No expansions returned — fall back to the type=query entry
            // (the original input's parse), which still gives us
            // case-normalized + reordered components.
            return entries.FirstOrDefault(e =>
                string.Equals(e.Type, "query", StringComparison.OrdinalIgnoreCase));
        }

        return expansions
            .OrderByDescending(e => GetRoadLength(e))
            .ThenBy(e => e.Data, StringComparer.Ordinal)
            .First();
    }

    private static int GetRoadLength(ExpandParseEntry entry)
    {
        var road = entry.Parsed?.FirstOrDefault(p =>
            string.Equals(p.Label, "road", StringComparison.OrdinalIgnoreCase))?.Value;
        return road?.Length ?? 0;
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

    private sealed record ExpandParseEntry(
        [property: JsonPropertyName("data")] string? Data,
        [property: JsonPropertyName("parsed")] List<ParsedComponent>? Parsed,
        [property: JsonPropertyName("type")] string? Type);
}

public sealed class LibpostalOptions
{
    public const string SectionName = "Libpostal";

    /// <summary>Base URL of the libpostal-rest sidecar (e.g. <c>http://libpostal:8080</c>).</summary>
    public string BaseUrl { get; set; } = "http://libpostal:8080";

    /// <summary>Per-request HTTP timeout. Defaults to 5 seconds — libpostal is fast.</summary>
    public int TimeoutSeconds { get; set; } = 5;
}
