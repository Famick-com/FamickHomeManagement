namespace Famick.HomeManagement.Core.DTOs.Common;

/// <summary>
/// Result from address normalization/geocoding service
/// </summary>
public class NormalizedAddressResult
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GeoapifyPlaceId { get; set; }
    public string? FormattedAddress { get; set; }

    /// <summary>
    /// Confidence score from 0 to 1 indicating match quality
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// The type of match (e.g., "building", "street", "city")
    /// </summary>
    public string? MatchType { get; set; }
}
