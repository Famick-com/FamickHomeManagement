namespace Famick.HomeManagement.Core.DTOs.Home;

/// <summary>
/// Request for initial home setup wizard.
/// Contains minimal required data: Property Basics + HVAC filter + battery type
/// </summary>
public class HomeSetupRequest
{
    #region Property Basics (Step 1)

    /// <summary>
    /// Unit or apartment number (optional)
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Year the property was built
    /// </summary>
    public int? YearBuilt { get; set; }

    /// <summary>
    /// Total square footage
    /// </summary>
    public int? SquareFootage { get; set; }

    /// <summary>
    /// Number of bedrooms
    /// </summary>
    public int? Bedrooms { get; set; }

    /// <summary>
    /// Number of bathrooms (supports half baths)
    /// </summary>
    public decimal? Bathrooms { get; set; }

    /// <summary>
    /// HOA name (if applicable)
    /// </summary>
    public string? HoaName { get; set; }

    /// <summary>
    /// HOA contact information
    /// </summary>
    public string? HoaContactInfo { get; set; }

    /// <summary>
    /// Link to HOA rules/documents
    /// </summary>
    public string? HoaRulesLink { get; set; }

    #endregion

    #region HVAC (Step 2)

    /// <summary>
    /// AC filter size(s) - comma-separated if multiple
    /// </summary>
    public string? AcFilterSizes { get; set; }

    #endregion

    #region Safety (Step 3)

    /// <summary>
    /// Smoke/CO detector battery type
    /// </summary>
    public string? SmokeCoDetectorBatteryType { get; set; }

    #endregion
}
