namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Lightweight vehicle summary for list views
/// </summary>
public class VehicleSummaryDto
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Trim { get; set; }
    public string? LicensePlate { get; set; }
    public string? Color { get; set; }
    public int? CurrentMileage { get; set; }
    public string? PrimaryDriverName { get; set; }
    public bool IsActive { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Next upcoming maintenance due date (if any)
    /// </summary>
    public DateTime? NextMaintenanceDueDate { get; set; }

    /// <summary>
    /// Next upcoming maintenance due mileage (if any)
    /// </summary>
    public int? NextMaintenanceDueMileage { get; set; }
}
