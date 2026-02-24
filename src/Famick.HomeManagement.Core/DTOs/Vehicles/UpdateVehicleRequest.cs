namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Request to update an existing vehicle
/// </summary>
public class UpdateVehicleRequest
{
    /// <summary>
    /// Model year of the vehicle
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Manufacturer/brand
    /// </summary>
    public string Make { get; set; } = string.Empty;

    /// <summary>
    /// Model name
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Trim level
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

    /// <summary>
    /// Current odometer reading
    /// </summary>
    public int? CurrentMileage { get; set; }

    /// <summary>
    /// Primary driver contact ID
    /// </summary>
    public Guid? PrimaryDriverContactId { get; set; }

    /// <summary>
    /// Date the vehicle was purchased
    /// </summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>
    /// Purchase price
    /// </summary>
    public decimal? PurchasePrice { get; set; }

    /// <summary>
    /// Where the vehicle was purchased
    /// </summary>
    public string? PurchaseLocation { get; set; }

    /// <summary>
    /// Notes about the vehicle
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether the vehicle is currently active/owned
    /// </summary>
    public bool IsActive { get; set; } = true;
}
