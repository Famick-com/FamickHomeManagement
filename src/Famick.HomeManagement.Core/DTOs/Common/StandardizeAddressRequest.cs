namespace Famick.HomeManagement.Core.DTOs.Common;

/// <summary>
/// Request payload for the manual-entry path: the user typed an address that
/// wasn't found in the autocomplete results. The server standardizes it via
/// the external provider (Smarty US Street) when available, then persists the
/// resulting address (with dedupe) and returns the saved <see cref="AddressDto"/>.
/// </summary>
public class StandardizeAddressRequest
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}
