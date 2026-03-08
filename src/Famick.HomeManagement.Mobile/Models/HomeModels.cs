namespace Famick.HomeManagement.Mobile.Models;

#region DTOs

public class MobileHomeDto
{
    public Guid Id { get; set; }

    // Property Basics
    public string? Unit { get; set; }
    public int? YearBuilt { get; set; }
    public int? SquareFootage { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    public string? HoaName { get; set; }
    public string? HoaContactInfo { get; set; }
    public string? HoaRulesLink { get; set; }

    // HVAC
    public string? AcFilterSizes { get; set; }

    // Maintenance & Consumables
    public int? AcFilterReplacementIntervalDays { get; set; }
    public string? FridgeWaterFilterType { get; set; }
    public string? UnderSinkFilterType { get; set; }
    public string? WholeHouseFilterType { get; set; }
    public string? SmokeCoDetectorBatteryType { get; set; }
    public string? HvacServiceSchedule { get; set; }
    public string? PestControlSchedule { get; set; }

    // Insurance & Financial
    public int? InsuranceType { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public string? InsuranceAgentName { get; set; }
    public string? InsuranceAgentPhone { get; set; }
    public string? InsuranceAgentEmail { get; set; }
    public string? MortgageInfo { get; set; }
    public string? PropertyTaxAccountNumber { get; set; }
    public string? EscrowDetails { get; set; }
    public decimal? AppraisalValue { get; set; }
    public DateTime? AppraisalDate { get; set; }

    // Setup Status
    public bool IsSetupComplete { get; set; }

    // Related Data
    public List<MobileHomeUtilityDto> Utilities { get; set; } = new();

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MobileHomeUtilityDto
{
    public Guid Id { get; set; }
    public int UtilityType { get; set; }
    public string? UtilityTypeName { get; set; }
    public string? CompanyName { get; set; }
    public string? AccountNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? LoginEmail { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string DisplayName => CompanyName ?? UtilityTypeHelper.GetDisplayName(UtilityType);
}

#endregion

#region Requests

public class UpdateHomeMobileRequest
{
    // Property Basics
    public string? Unit { get; set; }
    public int? YearBuilt { get; set; }
    public int? SquareFootage { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    public string? HoaName { get; set; }
    public string? HoaContactInfo { get; set; }
    public string? HoaRulesLink { get; set; }

    // HVAC
    public string? AcFilterSizes { get; set; }

    // Maintenance & Consumables
    public int? AcFilterReplacementIntervalDays { get; set; }
    public string? FridgeWaterFilterType { get; set; }
    public string? UnderSinkFilterType { get; set; }
    public string? WholeHouseFilterType { get; set; }
    public string? SmokeCoDetectorBatteryType { get; set; }
    public string? HvacServiceSchedule { get; set; }
    public string? PestControlSchedule { get; set; }

    // Insurance & Financial
    public int? InsuranceType { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public string? InsuranceAgentName { get; set; }
    public string? InsuranceAgentPhone { get; set; }
    public string? InsuranceAgentEmail { get; set; }
    public string? MortgageInfo { get; set; }
    public string? PropertyTaxAccountNumber { get; set; }
    public string? EscrowDetails { get; set; }
    public decimal? AppraisalValue { get; set; }
    public DateTime? AppraisalDate { get; set; }
}

public class CreateUtilityMobileRequest
{
    public int UtilityType { get; set; }
    public string? CompanyName { get; set; }
    public string? AccountNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? LoginEmail { get; set; }
    public string? Notes { get; set; }
}

public class UpdateUtilityMobileRequest
{
    public string? CompanyName { get; set; }
    public string? AccountNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? LoginEmail { get; set; }
    public string? Notes { get; set; }
}

#endregion

#region Helpers

public static class UtilityTypeHelper
{
    private static readonly Dictionary<int, string> DisplayNames = new()
    {
        { 0, "Electric" },
        { 1, "Gas" },
        { 2, "Water / Sewer" },
        { 3, "Trash / Recycling" },
        { 4, "Internet" },
        { 5, "TV / Streaming" },
        { 6, "Security System" },
        { 7, "HOA Dues / Portal" }
    };

    public static string GetDisplayName(int type) =>
        DisplayNames.TryGetValue(type, out var name) ? name : $"Other ({type})";

    public static List<string> AllDisplayNames => DisplayNames.Values.ToList();

    public static int GetTypeFromIndex(int index) => index;
}

public static class InsuranceTypeHelper
{
    private static readonly Dictionary<int, string> DisplayNames = new()
    {
        { 0, "Homeowners" },
        { 1, "Renters" }
    };

    public static string GetDisplayName(int? type) =>
        type.HasValue && DisplayNames.TryGetValue(type.Value, out var name) ? name : "Not set";

    public static List<string> AllDisplayNames => DisplayNames.Values.ToList();

    public static int? GetTypeFromIndex(int index) => index >= 0 && index < DisplayNames.Count ? index : null;
}

public record UtilityPopupResult(
    int UtilityType,
    string? CompanyName,
    string? AccountNumber,
    string? PhoneNumber,
    string? Website,
    string? LoginEmail,
    string? Notes);

#endregion
