namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Provider-agnostic abstraction for external address autocomplete and
/// standardization. Self-hosted deployments wire this up to Geoapify
/// (request-limited free tier, easy setup). Cloud deployments wire it up to
/// Smarty (US-only, USPS-grade). Selection happens via the
/// <c>AddressAutocomplete:Provider</c> config key.
///
/// Implementations MUST swallow provider-level failures (missing credentials,
/// HTTP errors, timeouts) and return a safe default so callers can continue
/// serving local-only results.
/// </summary>
public interface IAddressAutocompleteProvider
{
    /// <summary>
    /// A short identifier describing which provider is active (e.g. "Smarty",
    /// "Geoapify"). Stored on cached suggestions so the UI / logs can
    /// attribute hits back to the upstream service.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Suggests addresses for the given prefix. Returns an empty list when
    /// credentials are not configured or the provider is unavailable.
    /// </summary>
    Task<List<ExternalAddressSuggestion>> AutocompleteAsync(
        string prefix,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Standardizes a fully-specified address via the provider. Returns null
    /// when the provider returns no match, credentials are not configured, or
    /// the provider call fails.
    /// </summary>
    Task<ExternalStandardizedAddress?> StandardizeAsync(
        ExternalStandardizeInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single autocomplete suggestion returned from an external provider.
/// </summary>
public class ExternalAddressSuggestion
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>
    /// Provider-assigned identifier the implementation can use to fetch the
    /// full canonical record on standardize (e.g. Geoapify's <c>place_id</c>).
    /// Opaque to callers.
    /// </summary>
    public string? ProviderPlaceId { get; set; }

    public string FormattedText =>
        string.Join(", ",
            new[] { Line1, Line2, City, State, PostalCode }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
}

public class ExternalStandardizeInput
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    /// <summary>
    /// When set, the provider should prefer looking up the canonical record
    /// by this id rather than by the free-text fields. Useful when resolving
    /// a previously-cached suggestion.
    /// </summary>
    public string? ProviderPlaceId { get; set; }
}

public class ExternalStandardizedAddress
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ProviderPlaceId { get; set; }
    public string? FormattedAddress { get; set; }
}
