using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Home;

/// <summary>
/// Request for creating a new home utility
/// </summary>
public class CreateHomeUtilityRequest
{
    public UtilityType UtilityType { get; set; }
    public string? CompanyName { get; set; }
    public string? AccountNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? LoginEmail { get; set; }
    public string? Notes { get; set; }
}
