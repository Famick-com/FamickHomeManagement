using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Request to add an address to a contact
/// </summary>
public class AddContactAddressRequest
{
    /// <summary>
    /// Existing address ID to link (if reusing an existing address)
    /// </summary>
    public Guid? AddressId { get; set; }

    #region New Address Fields (used if AddressId is null)

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
    public string? AddressLine4 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }

    // Geoapify normalization fields
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GeoapifyPlaceId { get; set; }
    public string? FormattedAddress { get; set; }

    #endregion

    /// <summary>
    /// Address tag (Home, Work, etc.)
    /// </summary>
    public AddressTag Tag { get; set; } = AddressTag.Home;

    /// <summary>
    /// Whether to set this as the primary address
    /// </summary>
    public bool IsPrimary { get; set; } = false;
}
