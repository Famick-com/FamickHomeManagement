using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Contact address with tag information
/// </summary>
public class ContactAddressDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public Guid AddressId { get; set; }

    /// <summary>
    /// The full address details
    /// </summary>
    public AddressDto Address { get; set; } = null!;

    /// <summary>
    /// Address tag (Home, Work, etc.)
    /// </summary>
    public AddressTag Tag { get; set; }

    /// <summary>
    /// Whether this is the primary address
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Whether this address is the tenant's address (read-only in UI)
    /// </summary>
    public bool IsTenantAddress { get; set; }

    public DateTime CreatedAt { get; set; }
}
