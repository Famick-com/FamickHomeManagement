using Famick.HomeManagement.Core.DTOs.Common;

namespace Famick.HomeManagement.Core.DTOs.Tenant;

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? AddressId { get; set; }
    public AddressDto? Address { get; set; }
    public string TimeZoneId { get; set; } = "America/New_York";
    public string DefaultEventColor { get; set; } = "#4CAF50";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
