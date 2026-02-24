using Famick.HomeManagement.Core.DTOs.Home;
using Famick.HomeManagement.Core.DTOs.Vehicles;

namespace Famick.HomeManagement.Core.DTOs.Wizard;

/// <summary>
/// Complete wizard state containing data for all 5 pages.
/// Used to load existing data when re-running the wizard from settings.
/// </summary>
public class WizardStateDto
{
    /// <summary>
    /// Whether the wizard has been completed at least once
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Page 1: Household Info
    /// </summary>
    public HouseholdInfoDto HouseholdInfo { get; set; } = new();

    /// <summary>
    /// Page 2: Household Members
    /// </summary>
    public List<HouseholdMemberDto> HouseholdMembers { get; set; } = new();

    /// <summary>
    /// Page 3: Home Statistics
    /// </summary>
    public HomeStatisticsDto HomeStatistics { get; set; } = new();

    /// <summary>
    /// Page 4: Maintenance Items (as Equipment)
    /// Note: Equipment is handled separately via existing EquipmentController
    /// </summary>
    public MaintenanceItemsDto MaintenanceItems { get; set; } = new();

    /// <summary>
    /// Page 5: Vehicles
    /// </summary>
    public List<VehicleSummaryDto> Vehicles { get; set; } = new();
}

/// <summary>
/// Page 1: Household Info (name + address)
/// </summary>
public class HouseholdInfoDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;

    // Address fields
    public string? Street1 { get; set; }
    public string? Street2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    /// <summary>
    /// Whether the address has been normalized
    /// </summary>
    public bool IsAddressNormalized { get; set; }
}

/// <summary>
/// Page 3: Home Statistics
/// </summary>
public class HomeStatisticsDto
{
    public Guid? HomeId { get; set; }
    public int? SquareFootage { get; set; }
    public int? YearBuilt { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    public string? Unit { get; set; }

    // HOA Info
    public string? HoaName { get; set; }
    public string? HoaContactInfo { get; set; }
    public string? HoaRulesLink { get; set; }

    // Property Links
    public List<PropertyLinkDto> PropertyLinks { get; set; } = new();
}

/// <summary>
/// Page 4: Maintenance Items summary
/// </summary>
public class MaintenanceItemsDto
{
    // HVAC
    public string? AcFilterSizes { get; set; }
    public string? HeatingType { get; set; }
    public string? AcType { get; set; }

    // Water Systems
    public string? FridgeWaterFilterType { get; set; }
    public string? UnderSinkFilterType { get; set; }
    public string? WholeHouseFilterType { get; set; }
    public string? WaterHeaterSize { get; set; }
    public string? WaterHeaterType { get; set; }

    // Safety
    public string? SmokeCoDetectorBatteryType { get; set; }
}
