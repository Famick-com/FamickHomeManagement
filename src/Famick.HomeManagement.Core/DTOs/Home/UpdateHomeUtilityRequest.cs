namespace Famick.HomeManagement.Core.DTOs.Home;

/// <summary>
/// Request for updating an existing home utility
/// </summary>
public class UpdateHomeUtilityRequest
{
    public string? CompanyName { get; set; }
    public string? AccountNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? LoginEmail { get; set; }
    public string? Notes { get; set; }
}
