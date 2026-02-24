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
    /// Geoapify place ID for reference
    /// </summary>
    public string? GeoapifyPlaceId { get; set; }

    /// <summary>
    /// Geoapify's formatted version of the address
    /// </summary>
    public string? FormattedAddress { get; set; }

    /// <summary>
    /// Normalized hash of address components for duplicate detection.
    /// Generated from lowercase, trimmed: line1|city|state|postal|country
    /// </summary>
    public string? NormalizedHash { get; set; }
}
