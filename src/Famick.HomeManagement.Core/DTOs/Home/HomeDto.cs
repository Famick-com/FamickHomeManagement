using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Home;

/// <summary>
/// Full home data transfer object with all sections
/// </summary>
public class HomeDto
{
    public Guid Id { get; set; }

    #region Property Basics

    public string? Unit { get; set; }
    public int? YearBuilt { get; set; }
    public int? SquareFootage { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    public string? HoaName { get; set; }
    public string? HoaContactInfo { get; set; }
    public string? HoaRulesLink { get; set; }

    #endregion

    #region HVAC

    public string? AcFilterSizes { get; set; }

    #endregion

    #region Maintenance & Consumables

    public int? AcFilterReplacementIntervalDays { get; set; }
    public string? FridgeWaterFilterType { get; set; }
    public string? UnderSinkFilterType { get; set; }
    public string? WholeHouseFilterType { get; set; }
    public string? SmokeCoDetectorBatteryType { get; set; }
    public string? HvacServiceSchedule { get; set; }
    public string? PestControlSchedule { get; set; }

    #endregion

    #region Insurance & Financial

    public InsuranceType? InsuranceType { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public string? InsuranceAgentName { get; set; }
    public string? InsuranceAgentPhone { get; set; }
    public string? InsuranceAgentEmail { get; set; }
    public string? MortgageInfo { get; set; }
    public string? PropertyTaxAccountNumber { get; set; }
    public string? EscrowDetails { get; set; }
    public decimal? AppraisalValue { get; set; }
    public DateTime? AppraisalDate { get; set; }

    #endregion

    #region Setup Status

    public bool IsSetupComplete { get; set; }

    #endregion

    #region Related Data

    public List<HomeUtilityDto> Utilities { get; set; } = new();

    #endregion

    #region Audit

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    #endregion
}
