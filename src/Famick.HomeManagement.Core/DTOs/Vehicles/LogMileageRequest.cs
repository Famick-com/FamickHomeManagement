namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Request to log a mileage reading
/// </summary>
public class LogMileageRequest
{
    /// <summary>
    /// Odometer reading
    /// </summary>
    public int Mileage { get; set; }

    /// <summary>
    /// Date of the reading (defaults to now if not provided)
    /// </summary>
    public DateTime? ReadingDate { get; set; }

    /// <summary>
    /// Optional notes
    /// </summary>
    public string? Notes { get; set; }
}
