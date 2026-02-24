namespace Famick.HomeManagement.Mobile.Models;

#region Wizard State

public class WizardStateDto
{
    public bool IsComplete { get; set; }
    public HouseholdInfoDto HouseholdInfo { get; set; } = new();
    public List<HouseholdMemberDto> HouseholdMembers { get; set; } = new();
    public HomeStatisticsDto HomeStatistics { get; set; } = new();
    public MaintenanceItemsDto MaintenanceItems { get; set; } = new();
    public List<VehicleSummaryDto> Vehicles { get; set; } = new();
}

public class HouseholdInfoDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Street1 { get; set; }
    public string? Street2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool IsAddressNormalized { get; set; }
}

public class HomeStatisticsDto
{
    public Guid? HomeId { get; set; }
    public int? SquareFootage { get; set; }
    public int? YearBuilt { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    public string? Unit { get; set; }
    public string? HoaName { get; set; }
    public string? HoaContactInfo { get; set; }
    public string? HoaRulesLink { get; set; }
    public List<PropertyLinkDto> PropertyLinks { get; set; } = new();
}

public class MaintenanceItemsDto
{
    public string? AcFilterSizes { get; set; }
    public string? HeatingType { get; set; }
    public string? AcType { get; set; }
    public string? FridgeWaterFilterType { get; set; }
    public string? UnderSinkFilterType { get; set; }
    public string? WholeHouseFilterType { get; set; }
    public string? WaterHeaterSize { get; set; }
    public string? WaterHeaterType { get; set; }
    public string? SmokeCoDetectorBatteryType { get; set; }
}

#endregion

#region Household Members

public class HouseholdMemberDto
{
    public Guid ContactId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfileImageFileName { get; set; }
    public string? RelationshipType { get; set; }
    public bool IsCurrentUser { get; set; }
    public bool HasUserAccount { get; set; }
    public string? Email { get; set; }
}

public class AddHouseholdMemberRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? RelationshipType { get; set; }
    public Guid? ExistingContactId { get; set; }
}

public class UpdateHouseholdMemberRequest
{
    public string? RelationshipType { get; set; }
}

public class SaveCurrentUserContactRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
}

public class CheckDuplicateContactRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
}

public class DuplicateContactResultDto
{
    public bool HasDuplicates { get; set; }
    public List<DuplicateContactMatchDto> Matches { get; set; } = new();
}

public class DuplicateContactMatchDto
{
    public Guid ContactId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfileImageFileName { get; set; }
    public bool IsHouseholdMember { get; set; }
    public string MatchType { get; set; } = "Exact";
}

#endregion

#region Vehicles

public class VehicleSummaryDto
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Trim { get; set; }
    public string? LicensePlate { get; set; }
    public string? Color { get; set; }
    public int? CurrentMileage { get; set; }
    public string? PrimaryDriverName { get; set; }
    public bool IsActive { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime? NextMaintenanceDueDate { get; set; }
    public int? NextMaintenanceDueMileage { get; set; }
}

public class CreateVehicleRequest
{
    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Trim { get; set; }
    public string? Vin { get; set; }
    public string? LicensePlate { get; set; }
    public string? Color { get; set; }
    public int? CurrentMileage { get; set; }
    public Guid? PrimaryDriverContactId { get; set; }
}

public class UpdateVehicleRequest
{
    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Trim { get; set; }
    public string? Vin { get; set; }
    public string? LicensePlate { get; set; }
    public string? Color { get; set; }
    public int? CurrentMileage { get; set; }
    public Guid? PrimaryDriverContactId { get; set; }
    public bool IsActive { get; set; } = true;
}

#endregion

#region Property Links

public class PropertyLinkDto
{
    public Guid Id { get; set; }
    public Guid HomeId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreatePropertyLinkRequest
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

#endregion

#region Address Normalization

public class NormalizeAddressRequest
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class NormalizedAddressResult
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GeoapifyPlaceId { get; set; }
    public string? FormattedAddress { get; set; }
    public double Confidence { get; set; }
    public string? MatchType { get; set; }
}

#endregion
