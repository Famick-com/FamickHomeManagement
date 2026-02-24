namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Full vehicle data transfer object
/// </summary>
public class VehicleDto
{
    public Guid Id { get; set; }

    #region Vehicle Identification

    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Trim { get; set; }
    public string? Vin { get; set; }
    public string? LicensePlate { get; set; }
    public string? Color { get; set; }

    #endregion

    #region Mileage

    public int? CurrentMileage { get; set; }
    public DateTime? MileageAsOfDate { get; set; }

    #endregion

    #region Ownership

    public Guid? PrimaryDriverContactId { get; set; }
    public string? PrimaryDriverName { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? PurchaseLocation { get; set; }

    #endregion

    #region Status

    public bool IsActive { get; set; }
    public string? Notes { get; set; }

    #endregion

    #region Computed

    public string DisplayName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    #endregion

    #region Related Data

    public List<VehicleMaintenanceScheduleDto> MaintenanceSchedules { get; set; } = new();

    #endregion

    #region Audit

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    #endregion
}
