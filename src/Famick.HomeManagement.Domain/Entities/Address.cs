namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a physical address. This is a shared entity (not tenant-scoped)
/// to enable address deduplication and reuse across contacts, vendors, etc.
/// </summary>
public class Address : BaseEntity
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
    public string? AddressLine4 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code (e.g., "US", "CA", "GB")
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// Latitude coordinate from geocoding
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate from geocoding
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Provider-issued opaque identifier for the verified address (e.g.
    /// Geoapify's place ID, Smarty's smarty_key). Pairs with
    /// <see cref="ProviderSource"/> — both are non-null for verified rows
    /// and null for hand-entered ones.
    /// </summary>
    public string? ProviderPlaceId { get; set; }

    /// <summary>
    /// Name of the verifying provider, mirroring
    /// <c>IAddressAutocompleteProvider.ProviderName</c> ("Geoapify",
    /// "Smarty", etc). Null for hand-entered rows that haven't been
    /// verified by any provider — those skip libpostal canonicalization.
    /// </summary>
    public string? ProviderSource { get; set; }

    /// <summary>
    /// Provider's formatted version of the address (display-only)
    /// </summary>
    public string? FormattedAddress { get; set; }

    /// <summary>
    /// Normalized hash of address components for duplicate detection.
    /// Generated from lowercase, trimmed: line1|city|state|postal|country
    /// </summary>
    public string? NormalizedHash { get; set; }
}
