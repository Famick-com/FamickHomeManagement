namespace Famick.HomeManagement.Core.DTOs.Common;

public class AddressDto
{
    public Guid Id { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
    public string? AddressLine4 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ProviderPlaceId { get; set; }
    /// <summary>Name of the verifying provider ("Smarty", "Geoapify",
    /// "Unknown") or null for hand-entered rows.</summary>
    public string? ProviderSource { get; set; }
    public string? FormattedAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Server-supplied UI hint for the contact's per-contact apt/suite
    /// (Line 2). Populated by <c>ResolveSuggestionAsync</c> when the
    /// resolved suggestion was a Smarty secondary expansion or carried a
    /// caller-provided override. Never persisted on the shared
    /// <c>Address</c> row — apt/suite is per-contact and lives on
    /// <c>ContactAddress</c>.
    /// </summary>
    public string? SuggestedLine2 { get; set; }

    /// <summary>
    /// Formatted single-line display of the address
    /// </summary>
    public string DisplayAddress => FormatDisplayAddress();

    private string FormatDisplayAddress()
    {
        if (!string.IsNullOrWhiteSpace(FormattedAddress))
            return FormattedAddress;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(AddressLine1)) parts.Add(AddressLine1);
        if (!string.IsNullOrWhiteSpace(AddressLine2)) parts.Add(AddressLine2);

        var cityStateZip = new List<string>();
        if (!string.IsNullOrWhiteSpace(City)) cityStateZip.Add(City);
        if (!string.IsNullOrWhiteSpace(StateProvince)) cityStateZip.Add(StateProvince);
        if (!string.IsNullOrWhiteSpace(PostalCode)) cityStateZip.Add(PostalCode);

        if (cityStateZip.Count > 0) parts.Add(string.Join(", ", cityStateZip));
        if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country);

        return string.Join(", ", parts);
    }
}
