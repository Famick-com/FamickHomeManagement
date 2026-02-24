namespace Famick.HomeManagement.Core.DTOs.Common;

/// <summary>
/// Request to normalize/geocode an address via Geoapify
/// </summary>
public class NormalizeAddressRequest
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}
