namespace Famick.HomeManagement.Mobile.Models;

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
    public string? GeoapifyPlaceId { get; set; }
    public string? FormattedAddress { get; set; }

    public string DisplayAddress
    {
        get
        {
            if (!string.IsNullOrEmpty(FormattedAddress)) return FormattedAddress;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(AddressLine1)) parts.Add(AddressLine1);
            if (!string.IsNullOrEmpty(AddressLine2)) parts.Add(AddressLine2);
            var cityState = string.Join(", ",
                new[] { City, StateProvince }.Where(s => !string.IsNullOrEmpty(s)));
            if (!string.IsNullOrEmpty(cityState))
            {
                if (!string.IsNullOrEmpty(PostalCode))
                    cityState += " " + PostalCode;
                parts.Add(cityState);
            }
            if (!string.IsNullOrEmpty(Country)) parts.Add(Country);
            return string.Join("\n", parts);
        }
    }
}
