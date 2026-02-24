namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request model for creating a new equipment usage log entry
/// </summary>
public class CreateEquipmentUsageLogRequest
{
    /// <summary>
    /// Date of the usage reading
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Usage reading value (miles, hours, cycles, etc.)
    /// </summary>
    public decimal Reading { get; set; }

    /// <summary>
    /// Optional notes about this reading
    /// </summary>
    public string? Notes { get; set; }
}
