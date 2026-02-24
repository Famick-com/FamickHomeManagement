namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Mileage log entry data transfer object
/// </summary>
public class VehicleMileageLogDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public int Mileage { get; set; }
    public DateTime ReadingDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
