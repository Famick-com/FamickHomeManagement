using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// <see cref="IAddressAutocompleteProvider"/> implementation backed by
/// Geoapify. The default for self-hosted deployments: simpler setup than
/// Smarty (single API key, request-limited free tier, international coverage).
///
/// All failure modes (missing credentials, HTTP errors, timeouts,
/// deserialization issues) degrade gracefully to empty / null so the caller
/// can keep serving local-only results.
/// </summary>
public class GeoapifyAddressAutocompleteProvider : IAddressAutocompleteProvider
{
    private readonly HttpClient _httpClient;
    private readonly GeoapifyOptions _options;
    private readonly ILogger<GeoapifyAddressAutocompleteProvider> _logger;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeoapifyAddressAutocompleteProvider(
        HttpClient httpClient,
        IOptions<GeoapifyOptions> options,
        ILogger<GeoapifyAddressAutocompleteProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Geoapify";

    public async Task<List<ExternalAddressSuggestion>> AutocompleteAsync(
        string prefix,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return new();
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogDebug("Geoapify API key not configured; returning empty autocomplete list");
            return new();
        }

        limit = Math.Clamp(limit, 1, 20);
        var url = $"{_options.BaseUrl.TrimEnd('/')}/autocomplete" +
                  $"?text={Uri.EscapeDataString(prefix.Trim())}" +
                  $"&limit={limit}" +
                  $"&format=json" +
                  $"&apiKey={Uri.EscapeDataString(_options.ApiKey)}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Geoapify autocomplete returned status {StatusCode}", response.StatusCode);
                return new();
            }

            var payload = await response.Content.ReadFromJsonAsync<GeoapifyAutocompleteResponse>(
                JsonSerializerOptions, cancellationToken);

            if (payload?.Results == null || payload.Results.Count == 0)
                return new();

            return payload.Results
                .Select(MapToSuggestion)
                .Where(s => !string.IsNullOrWhiteSpace(s.Line1))
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geoapify autocomplete call failed for prefix '{Prefix}'", prefix);
            return new();
        }
    }

    public async Task<ExternalStandardizedAddress?> StandardizeAsync(
        ExternalStandardizeInput input,
        CancellationToken cancellationToken = default)
    {
        if (input == null) return null;
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogDebug("Geoapify API key not configured; skipping standardization");
            return null;
        }

        // When the UI resolved a suggestion, we already have Geoapify's
        // place_id — prefer the cheaper place-details lookup.
        if (!string.IsNullOrWhiteSpace(input.ProviderPlaceId))
        {
            var byIdResult = await FetchByPlaceId(input.ProviderPlaceId!, cancellationToken);
            if (byIdResult != null) return byIdResult;
            // Fall through to search if the place lookup missed for any reason.
        }

        var text = BuildSearchText(input);
        if (string.IsNullOrWhiteSpace(text)) return null;

        var url = $"{_options.BaseUrl.TrimEnd('/')}/search" +
                  $"?text={Uri.EscapeDataString(text)}" +
                  $"&limit=1&format=json" +
                  $"&apiKey={Uri.EscapeDataString(_options.ApiKey)}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Geoapify search returned status {StatusCode}", response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GeoapifyAutocompleteResponse>(
                JsonSerializerOptions, cancellationToken);

            var first = payload?.Results?.FirstOrDefault();
            return first != null ? MapToStandardized(first) : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geoapify search call failed");
            return null;
        }
    }

    private async Task<ExternalStandardizedAddress?> FetchByPlaceId(string placeId, CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/place-details" +
                  $"?id={Uri.EscapeDataString(placeId)}" +
                  $"&apiKey={Uri.EscapeDataString(_options.ApiKey)}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var payload = await response.Content.ReadFromJsonAsync<GeoapifyFeatureCollection>(
                JsonSerializerOptions, cancellationToken);

            var props = payload?.Features?.FirstOrDefault()?.Properties;
            return props != null ? MapToStandardized(props) : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static ExternalAddressSuggestion MapToSuggestion(GeoapifyProperties p) => new()
    {
        Line1 = BuildLine1(p) ?? p.AddressLine1 ?? string.Empty,
        Line2 = null, // Geoapify doesn't return unit/suite separately
        City = p.City,
        State = p.State,
        PostalCode = p.Postcode,
        Country = p.Country,
        CountryCode = p.CountryCode?.ToUpperInvariant(),
        Latitude = p.Lat,
        Longitude = p.Lon,
        ProviderPlaceId = p.PlaceId
    };

    private static ExternalStandardizedAddress MapToStandardized(GeoapifyProperties p) => new()
    {
        Line1 = BuildLine1(p) ?? p.AddressLine1,
        Line2 = null,
        City = p.City,
        State = p.State,
        PostalCode = p.Postcode,
        Country = p.Country,
        CountryCode = p.CountryCode?.ToUpperInvariant(),
        Latitude = p.Lat,
        Longitude = p.Lon,
        ProviderPlaceId = p.PlaceId,
        FormattedAddress = p.Formatted
    };

    private static string? BuildLine1(GeoapifyProperties p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.Housenumber)) parts.Add(p.Housenumber!);
        if (!string.IsNullOrWhiteSpace(p.Street)) parts.Add(p.Street!);
        return parts.Count > 0 ? string.Join(" ", parts) : null;
    }

    private static string BuildSearchText(ExternalStandardizeInput input)
    {
        var parts = new List<string?>
        {
            input.Line1,
            input.Line2,
            input.City,
            input.State,
            input.PostalCode,
            input.Country
        };
        return string.Join(", ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    #region Geoapify response DTOs (internal)

    /// <summary>JSON format (?format=json) — a flat array under "results".</summary>
    private class GeoapifyAutocompleteResponse
    {
        public List<GeoapifyProperties>? Results { get; set; }
    }

    /// <summary>GeoJSON shape (place-details endpoint).</summary>
    private class GeoapifyFeatureCollection
    {
        public List<GeoapifyFeature>? Features { get; set; }
    }

    private class GeoapifyFeature
    {
        public GeoapifyProperties? Properties { get; set; }
    }

    private class GeoapifyProperties
    {
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public string? Formatted { get; set; }

        [JsonPropertyName("address_line1")]
        public string? AddressLine1 { get; set; }

        [JsonPropertyName("address_line2")]
        public string? AddressLine2 { get; set; }

        public string? Country { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        public string? State { get; set; }
        public string? City { get; set; }
        public string? Postcode { get; set; }
        public string? Street { get; set; }
        public string? Housenumber { get; set; }

        [JsonPropertyName("place_id")]
        public string? PlaceId { get; set; }
    }

    #endregion
}
