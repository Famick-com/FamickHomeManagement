using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Home;

/// <summary>
/// Home utility data transfer object
/// </summary>
public class HomeUtilityDto
{
    public Guid Id { get; set; }
    public UtilityType UtilityType { get; set; }
    public string UtilityTypeName => UtilityType.ToString();
    public string? CompanyName { get; set; }
    public string? AccountNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? LoginEmail { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
