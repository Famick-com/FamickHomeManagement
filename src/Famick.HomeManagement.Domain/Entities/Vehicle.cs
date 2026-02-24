namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a vehicle owned by the household.
/// Supports mileage tracking, maintenance scheduling, and document storage.
/// </summary>
public class Vehicle : BaseTenantEntity
{
    #region Vehicle Identification

    /// <summary>
    /// Model year of the vehicle
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Manufacturer/brand (e.g., "Toyota", "Ford")
    /// </summary>
    public string Make { get; set; } = string.Empty;

    /// <summary>
    /// Model name (e.g., "Camry", "F-150")
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Trim level (e.g., "SE", "Limited", "XLT")
    /// </summary>
    public string? Trim { get; set; }

    /// <summary>
    /// Vehicle Identification Number
    /// </summary>
    public string? Vin { get; set; }

    /// <summary>
    /// License plate number
    /// </summary>
    public string? LicensePlate { get; set; }

    /// <summary>
    /// Vehicle color
    /// </summary>
    public string? Color { get; set; }

    #endregion

    #region Mileage Tracking

    /// <summary>
    /// Current odometer reading
    /// </summary>
    public int? CurrentMileage { get; set; }

    /// <summary>
    /// Date when the current mileage was recorded
    /// </summary>
    public DateTime? MileageAsOfDate { get; set; }

    #endregion

    #region Ownership

    /// <summary>
    /// Primary driver of this vehicle (FK to Contact)
    /// </summary>
    public Guid? PrimaryDriverContactId { get; set; }

    /// <summary>
    /// Date the vehicle was purchased/acquired
    /// </summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>
    /// Purchase price of the vehicle
    /// </summary>
    public decimal? PurchasePrice { get; set; }

    /// <summary>
    /// Where the vehicle was purchased (dealership name)
    /// </summary>
    public string? PurchaseLocation { get; set; }

    #endregion

    #region Notes

    /// <summary>
    /// Free-form notes about the vehicle
    /// </summary>
    public string? Notes { get; set; }

    #endregion

    #region Status

    /// <summary>
    /// Whether this vehicle is currently active/owned
    /// </summary>
    public bool IsActive { get; set; } = true;

    #endregion

    #region Navigation Properties

    /// <summary>
    /// The primary driver of this vehicle
    /// </summary>
    public virtual Contact? PrimaryDriver { get; set; }

    /// <summary>
    /// Mileage log entries for tracking odometer over time
    /// </summary>
    public virtual ICollection<VehicleMileageLog> MileageLogs { get; set; } = new List<VehicleMileageLog>();

    /// <summary>
    /// Documents associated with this vehicle (registration, insurance, etc.)
    /// </summary>
    public virtual ICollection<VehicleDocument> Documents { get; set; } = new List<VehicleDocument>();

    /// <summary>
    /// Completed maintenance records
    /// </summary>
    public virtual ICollection<VehicleMaintenanceRecord> MaintenanceRecords { get; set; } = new List<VehicleMaintenanceRecord>();

    /// <summary>
    /// Recurring maintenance schedules
    /// </summary>
    public virtual ICollection<VehicleMaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<VehicleMaintenanceSchedule>();

    #endregion

    /// <summary>
    /// Gets a display name for the vehicle (Year Make Model)
    /// </summary>
    public string DisplayName => $"{Year} {Make} {Model}".Trim();

    /// <summary>
    /// Gets the full name including trim if available
    /// </summary>
    public string FullName => string.IsNullOrWhiteSpace(Trim)
        ? DisplayName
        : $"{Year} {Make} {Model} {Trim}".Trim();
}
