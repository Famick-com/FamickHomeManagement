using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a home/property with its details, utilities, and maintenance information.
/// One home per tenant in self-hosted mode.
/// </summary>
public class Home : BaseTenantEntity
{
    #region Property Basics

    /// <summary>
    /// Unit or apartment number (if applicable)
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Year the property was built
    /// </summary>
    public int? YearBuilt { get; set; }

    /// <summary>
    /// Total square footage of the property
    /// </summary>
    public int? SquareFootage { get; set; }

    /// <summary>
    /// Number of bedrooms
    /// </summary>
    public int? Bedrooms { get; set; }

    /// <summary>
    /// Number of bathrooms (supports half baths like 2.5)
    /// </summary>
    public decimal? Bathrooms { get; set; }

    /// <summary>
    /// Name of the HOA (if applicable)
    /// </summary>
    public string? HoaName { get; set; }

    /// <summary>
    /// HOA contact information (phone, email, etc.)
    /// </summary>
    public string? HoaContactInfo { get; set; }

    /// <summary>
    /// Link to HOA rules/documents
    /// </summary>
    public string? HoaRulesLink { get; set; }

    #endregion

    #region HVAC

    /// <summary>
    /// AC filter size(s) - comma-separated if multiple (e.g., "20x25x1, 16x20x1")
    /// </summary>
    public string? AcFilterSizes { get; set; }

    #endregion

    #region Maintenance & Consumables

    /// <summary>
    /// AC filter replacement interval in days
    /// </summary>
    public int? AcFilterReplacementIntervalDays { get; set; }

    /// <summary>
    /// Refrigerator water filter type/model
    /// </summary>
    public string? FridgeWaterFilterType { get; set; }

    /// <summary>
    /// Under-sink water filter type/model
    /// </summary>
    public string? UnderSinkFilterType { get; set; }

    /// <summary>
    /// Whole-house water filter type/model
    /// </summary>
    public string? WholeHouseFilterType { get; set; }

    /// <summary>
    /// Smoke/CO detector battery type (e.g., "9V", "AA", "10-year sealed")
    /// </summary>
    public string? SmokeCoDetectorBatteryType { get; set; }

    /// <summary>
    /// HVAC service schedule notes
    /// </summary>
    public string? HvacServiceSchedule { get; set; }

    /// <summary>
    /// Pest control schedule notes
    /// </summary>
    public string? PestControlSchedule { get; set; }

    #endregion

    #region Insurance & Financial

    /// <summary>
    /// Type of insurance (Homeowners or Renters)
    /// </summary>
    public InsuranceType? InsuranceType { get; set; }

    /// <summary>
    /// Insurance policy number
    /// </summary>
    public string? InsurancePolicyNumber { get; set; }

    /// <summary>
    /// Insurance agent name
    /// </summary>
    public string? InsuranceAgentName { get; set; }

    /// <summary>
    /// Insurance agent phone number
    /// </summary>
    public string? InsuranceAgentPhone { get; set; }

    /// <summary>
    /// Insurance agent email address
    /// </summary>
    public string? InsuranceAgentEmail { get; set; }

    /// <summary>
    /// Mortgage or rent payment information/notes
    /// </summary>
    public string? MortgageInfo { get; set; }

    /// <summary>
    /// Property tax account number
    /// </summary>
    public string? PropertyTaxAccountNumber { get; set; }

    /// <summary>
    /// Escrow account details (if applicable)
    /// </summary>
    public string? EscrowDetails { get; set; }

    /// <summary>
    /// Most recent appraisal value
    /// </summary>
    public decimal? AppraisalValue { get; set; }

    /// <summary>
    /// Date of most recent appraisal
    /// </summary>
    public DateTime? AppraisalDate { get; set; }

    #endregion

    #region Setup Status

    /// <summary>
    /// Indicates whether the initial home setup wizard has been completed
    /// </summary>
    public bool IsSetupComplete { get; set; } = false;

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Utilities and services associated with this home
    /// </summary>
    public virtual ICollection<HomeUtility> Utilities { get; set; } = new List<HomeUtility>();

    /// <summary>
    /// External links associated with this property (county records, Zillow, etc.)
    /// </summary>
    public virtual ICollection<PropertyLink> PropertyLinks { get; set; } = new List<PropertyLink>();

    #endregion
}
