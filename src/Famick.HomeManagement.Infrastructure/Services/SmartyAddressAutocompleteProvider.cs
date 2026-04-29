using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// <see cref="IAddressAutocompleteProvider"/> implementation backed by
/// Smarty's US Address APIs: US Autocomplete Pro for suggestions as the user
/// types, and US Street for standardizing a fully-specified address to USPS
/// format. All failure modes (missing credentials, HTTP errors, timeouts,
/// deserialization issues) degrade gracefully to empty / null so the caller
/// can keep serving local-only results.
/// </summary>
public class SmartyAddressAutocompleteProvider : IAddressAutocompleteProvider
{
    private readonly HttpClient _httpClient;
    private readonly SmartyOptions _options;
    private readonly ILogger<SmartyAddressAutocompleteProvider> _logger;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SmartyAddressAutocompleteProvider(
        HttpClient httpClient,
        IOptions<SmartyOptions> options,
        ILogger<SmartyAddressAutocompleteProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Smarty";

    public async Task<List<ExternalAddressSuggestion>> AutocompleteAsync(
        string prefix,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return new();
        if (!HasCredentials())
        {
            _logger.LogDebug("Smarty credentials not configured; returning empty autocomplete list");
            return new();
        }

        limit = Math.Clamp(limit, 1, 10);
        var url = $"{_options.AutocompleteBaseUrl.TrimEnd('/')}/lookup" +
                  $"?search={Uri.EscapeDataString(prefix.Trim())}" +
                  $"&max_results={limit}" +
                  $"&auth-id={Uri.EscapeDataString(_options.AuthId)}" +
                  $"&auth-token={Uri.EscapeDataString(_options.AuthToken)}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Smarty autocomplete returned status {StatusCode}", response.StatusCode);
                return new();
            }

            var payload = await response.Content.ReadFromJsonAsync<SmartyAutocompleteResponse>(
                JsonSerializerOptions, cancellationToken);

            if (payload?.Suggestions == null || payload.Suggestions.Count == 0)
                return new();

            return payload.Suggestions
                .Where(s => !string.IsNullOrWhiteSpace(s.StreetLine))
                .Select(s => new ExternalAddressSuggestion
                {
                    Line1 = s.StreetLine!,
                    Line2 = string.IsNullOrWhiteSpace(s.Secondary) ? null : s.Secondary,
                    City = s.City,
                    State = s.State,
                    PostalCode = s.Zipcode,
                    Country = "USA",
                    CountryCode = "US"
                })
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Smarty autocomplete call failed for prefix '{Prefix}'", prefix);
            return new();
        }
    }

    public async Task<ExternalStandardizedAddress?> StandardizeAsync(
        ExternalStandardizeInput input,
        CancellationToken cancellationToken = default)
    {
        if (input == null) return null;
        if (!HasCredentials())
        {
            _logger.LogDebug("Smarty credentials not configured; skipping standardization");
            return null;
        }

        var qs = new List<string>
        {
            $"auth-id={Uri.EscapeDataString(_options.AuthId)}",
            $"auth-token={Uri.EscapeDataString(_options.AuthToken)}",
            "candidates=1"
        };
        AddIfPresent(qs, "street", input.Line1);
        AddIfPresent(qs, "secondary", input.Line2);
        AddIfPresent(qs, "city", input.City);
        AddIfPresent(qs, "state", input.State);
        AddIfPresent(qs, "zipcode", input.PostalCode);

        var url = $"{_options.StreetBaseUrl.TrimEnd('/')}/street-address?{string.Join("&", qs)}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Smarty US Street returned status {StatusCode}", response.StatusCode);
                return null;
            }

            var results = await response.Content.ReadFromJsonAsync<List<SmartyStreetResult>>(
                JsonSerializerOptions, cancellationToken);

            if (results == null || results.Count == 0)
                return null;

            var first = results[0];
            var zipCombined = string.IsNullOrWhiteSpace(first.Components?.Plus4Code)
                ? first.Components?.Zipcode
                : $"{first.Components?.Zipcode}-{first.Components?.Plus4Code}";

            return new ExternalStandardizedAddress
            {
                Line1 = first.DeliveryLine1,
                Line2 = first.DeliveryLine2,
                City = first.Components?.CityName,
                State = first.Components?.StateAbbreviation,
                PostalCode = zipCombined,
                Country = "USA",
                CountryCode = "US",
                Latitude = first.Metadata?.Latitude,
                Longitude = first.Metadata?.Longitude,
                FormattedAddress = BuildFormatted(first, zipCombined)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Smarty standardize call failed");
            return null;
        }
    }

    private static string BuildFormatted(SmartyStreetResult r, string? zipCombined)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.DeliveryLine1)) lines.Add(r.DeliveryLine1!);
        if (!string.IsNullOrWhiteSpace(r.DeliveryLine2)) lines.Add(r.DeliveryLine2!);

        var csz = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.Components?.CityName)) csz.Add(r.Components!.CityName!);
        if (!string.IsNullOrWhiteSpace(r.Components?.StateAbbreviation)) csz.Add(r.Components!.StateAbbreviation!);
        if (!string.IsNullOrWhiteSpace(zipCombined)) csz.Add(zipCombined);
        if (csz.Count > 0) lines.Add(string.Join(" ", csz));

        return string.Join(", ", lines);
    }

    private bool HasCredentials() =>
        !string.IsNullOrWhiteSpace(_options.AuthId) &&
        !string.IsNullOrWhiteSpace(_options.AuthToken);

    private static void AddIfPresent(List<string> qs, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            qs.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
    }

    #region Smarty response DTOs (internal)

    private class SmartyAutocompleteResponse
    {
        public List<SmartyAutocompleteItem>? Suggestions { get; set; }
    }

    private class SmartyAutocompleteItem
    {
        [JsonPropertyName("street_line")]
        public string? StreetLine { get; set; }

        public string? Secondary { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zipcode { get; set; }
    }

    private class SmartyStreetResult
    {
        [JsonPropertyName("delivery_line_1")]
        public string? DeliveryLine1 { get; set; }

        [JsonPropertyName("delivery_line_2")]
        public string? DeliveryLine2 { get; set; }

        public SmartyStreetComponents? Components { get; set; }
        public SmartyStreetMetadata? Metadata { get; set; }
    }

    private class SmartyStreetComponents
    {
        [JsonPropertyName("city_name")]
        public string? CityName { get; set; }

        [JsonPropertyName("state_abbreviation")]
        public string? StateAbbreviation { get; set; }

        public string? Zipcode { get; set; }

        [JsonPropertyName("plus4_code")]
        public string? Plus4Code { get; set; }
    }

    private class SmartyStreetMetadata
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    #endregion
}

/// <summary>
/// Configuration for the Smarty US Address APIs. Bound from the
/// <c>Smarty</c> section of configuration.
/// </summary>
public class SmartyOptions
{
    public const string SectionName = "Smarty";

    public string AuthId { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string AutocompleteBaseUrl { get; set; } = "https://us-autocomplete-pro.api.smarty.com";
    public string StreetBaseUrl { get; set; } = "https://us-street.api.smarty.com";
}
