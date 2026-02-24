using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Address normalization service using Geoapify Geocoding API
/// </summary>
public class GeoapifyAddressService : IAddressNormalizationService
{
    private readonly HttpClient _httpClient;
    private readonly GeoapifyOptions _options;
    private readonly ILogger<GeoapifyAddressService> _logger;

    public GeoapifyAddressService(
        HttpClient httpClient,
        IOptions<GeoapifyOptions> options,
        ILogger<GeoapifyAddressService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<NormalizedAddressResult?> NormalizeAsync(
        NormalizeAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var suggestions = await NormalizeSuggestionsAsync(request, 1, cancellationToken);
        return suggestions.FirstOrDefault();
    }

    public async Task<List<NormalizedAddressResult>> NormalizeSuggestionsAsync(
        NormalizeAddressRequest request,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        // Build the address text from components for logging/fallback
        var addressParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.AddressLine1)) addressParts.Add(request.AddressLine1);
        if (!string.IsNullOrWhiteSpace(request.AddressLine2)) addressParts.Add(request.AddressLine2);
        if (!string.IsNullOrWhiteSpace(request.City)) addressParts.Add(request.City);
        if (!string.IsNullOrWhiteSpace(request.StateProvince)) addressParts.Add(request.StateProvince);
        if (!string.IsNullOrWhiteSpace(request.PostalCode)) addressParts.Add(request.PostalCode);
        if (!string.IsNullOrWhiteSpace(request.Country)) addressParts.Add(request.Country);

        var addressText = string.Join(", ", addressParts);
        if (string.IsNullOrWhiteSpace(addressText))
        {
            _logger.LogDebug("Empty address provided, returning empty list");
            return new List<NormalizedAddressResult>();
        }

        // If no API key, return a fallback formatted result using the input
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Geoapify API key not configured, using fallback address formatting");
            var fallback = CreateFallbackResult(request);
            return new List<NormalizedAddressResult> { fallback };
        }

        try
        {
            var encodedAddress = Uri.EscapeDataString(addressText);
            var url = $"{_options.BaseUrl}/search?text={encodedAddress}&apiKey={_options.ApiKey}&format=json&limit={limit}";

            _logger.LogDebug("Calling Geoapify API for address: {Address} with limit {Limit}", addressText, limit);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Geoapify API returned status {StatusCode}", response.StatusCode);
                return new List<NormalizedAddressResult>();
            }

            var result = await response.Content.ReadFromJsonAsync<GeoapifyResponse>(
                JsonSerializerOptions,
                cancellationToken);

            if (result?.Results == null || result.Results.Count == 0)
            {
                _logger.LogDebug("No results returned from Geoapify for address: {Address}", addressText);
                return new List<NormalizedAddressResult>();
            }

            return result.Results
                .Select(MapToNormalizedResult)
                .OrderByDescending(r => r.Confidence)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Geoapify API for address: {Address}", addressText);
            return new List<NormalizedAddressResult>();
        }
    }

    private static NormalizedAddressResult MapToNormalizedResult(GeoapifyResult result)
    {
        return new NormalizedAddressResult
        {
            AddressLine1 = BuildAddressLine1(result),
            AddressLine2 = null, // Geoapify doesn't typically return apt/suite info separately
            City = result.City,
            StateProvince = result.State,
            PostalCode = result.Postcode,
            Country = result.Country,
            CountryCode = result.CountryCode?.ToUpperInvariant(),
            Latitude = result.Lat,
            Longitude = result.Lon,
            GeoapifyPlaceId = result.PlaceId,
            FormattedAddress = result.Formatted,
            Confidence = CalculateConfidence(result),
            MatchType = result.ResultType
        };
    }

    private static string? BuildAddressLine1(GeoapifyResult result)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.Housenumber))
            parts.Add(result.Housenumber);
        if (!string.IsNullOrWhiteSpace(result.Street))
            parts.Add(result.Street);

        return parts.Count > 0 ? string.Join(" ", parts) : result.AddressLine1;
    }

    private static double CalculateConfidence(GeoapifyResult result)
    {
        // Geoapify uses rank object with importance and confidence
        // Convert to 0-1 scale
        var confidence = result.Rank?.Confidence ?? 0;
        return Math.Min(1.0, Math.Max(0.0, confidence));
    }

    /// <summary>
    /// Creates a fallback normalized result when Geoapify API is not available.
    /// Formats the address using standard conventions without geocoding.
    /// </summary>
    private static NormalizedAddressResult CreateFallbackResult(NormalizeAddressRequest request)
    {
        // Build formatted address using standard format
        var formattedParts = new List<string>();

        // Line 1: Street address
        if (!string.IsNullOrWhiteSpace(request.AddressLine1))
            formattedParts.Add(request.AddressLine1);

        // Line 2: Apt/Suite (if present)
        if (!string.IsNullOrWhiteSpace(request.AddressLine2))
            formattedParts.Add(request.AddressLine2);

        // City, State PostalCode
        var cityStateZip = BuildCityStateZip(request.City, request.StateProvince, request.PostalCode);
        if (!string.IsNullOrWhiteSpace(cityStateZip))
            formattedParts.Add(cityStateZip);

        // Country
        if (!string.IsNullOrWhiteSpace(request.Country))
            formattedParts.Add(request.Country);

        return new NormalizedAddressResult
        {
            AddressLine1 = request.AddressLine1?.Trim(),
            AddressLine2 = request.AddressLine2?.Trim(),
            City = request.City?.Trim(),
            StateProvince = request.StateProvince?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            Country = request.Country?.Trim(),
            CountryCode = null, // Cannot determine without API
            Latitude = null,
            Longitude = null,
            GeoapifyPlaceId = null,
            FormattedAddress = string.Join(", ", formattedParts),
            Confidence = 1.0, // Full confidence since we're using user's input as-is
            MatchType = "fallback"
        };
    }

    private static string BuildCityStateZip(string? city, string? state, string? postalCode)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(city))
            parts.Add(city.Trim());

        if (!string.IsNullOrWhiteSpace(state))
            parts.Add(state.Trim());

        var cityState = string.Join(", ", parts);

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            return string.IsNullOrWhiteSpace(cityState)
                ? postalCode.Trim()
                : $"{cityState} {postalCode.Trim()}";
        }

        return cityState;
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    #region Geoapify Response DTOs

    private class GeoapifyResponse
    {
        public List<GeoapifyResult> Results { get; set; } = new();
    }

    private class GeoapifyResult
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

        [JsonPropertyName("result_type")]
        public string? ResultType { get; set; }

        [JsonPropertyName("place_id")]
        public string? PlaceId { get; set; }

        public GeoapifyRank? Rank { get; set; }
    }

    private class GeoapifyRank
    {
        public double Importance { get; set; }
        public double Confidence { get; set; }
    }

    #endregion
}

/// <summary>
/// Configuration options for Geoapify API
/// </summary>
public class GeoapifyOptions
{
    public const string SectionName = "Geoapify";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.geoapify.com/v1/geocode";
}
