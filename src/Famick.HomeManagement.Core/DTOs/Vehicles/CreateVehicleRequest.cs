namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Request to create a new vehicle
/// </summary>
public class CreateVehicleRequest
{
    /// <summary>
    /// Model year of the vehicle (required)
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Manufacturer/brand (required)
    /// </summary>
    public string Make { get; set; } = string.Empty;

    /// <summary>
    /// Model name (required)
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Trim level (optional)
    /// </summary>
    public string? Trim { get; set; }

    /// <summary>
    /// Vehicle Identification Number (optional)
    /// </summary>
    public string? Vin { get; set; }

    /// <summary>
    /// License plate number (optional)
    /// </summary>
    public string? LicensePlate { get; set; }

    /// <summary>
    /// Vehicle color (optional)
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Current odometer reading (optional)
    /// </summary>
    public int? CurrentMileage { get; set; }

    /// <summary>
    /// Primary driver contact ID (optional)
    /// </summary>
    public Guid? PrimaryDriverContactId { get; set; }

    /// <summary>
    /// Date the vehicle was purchased (optional)
    /// </summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>
    /// Purchase price (optional)
    /// </summary>
    public decimal? PurchasePrice { get; set; }

    /// <summary>
    /// Where the vehicle was purchased (optional)
    /// </summary>
    public string? PurchaseLocation { get; set; }

    /// <summary>
    /// Notes about the vehicle (optional)
    /// </summary>
    public string? Notes { get; set; }
}
