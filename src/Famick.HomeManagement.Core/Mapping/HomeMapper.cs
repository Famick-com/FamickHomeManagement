#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Home;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class HomeMapper
{
    public static partial HomeDto ToDto(Home source);

    public static partial PropertyLinkDto ToPropertyLinkDto(PropertyLink source);

    [MapperIgnoreTarget(nameof(Home.Id))]
    [MapperIgnoreTarget(nameof(Home.TenantId))]
    [MapperIgnoreTarget(nameof(Home.CreatedAt))]
    [MapperIgnoreTarget(nameof(Home.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Home.IsSetupComplete))]
    [MapperIgnoreTarget(nameof(Home.Utilities))]
    [MapperIgnoreTarget(nameof(Home.PropertyLinks))]
    [MapperIgnoreTarget(nameof(Home.AcFilterReplacementIntervalDays))]
    [MapperIgnoreTarget(nameof(Home.FridgeWaterFilterType))]
    [MapperIgnoreTarget(nameof(Home.UnderSinkFilterType))]
    [MapperIgnoreTarget(nameof(Home.WholeHouseFilterType))]
    [MapperIgnoreTarget(nameof(Home.HvacServiceSchedule))]
    [MapperIgnoreTarget(nameof(Home.PestControlSchedule))]
    [MapperIgnoreTarget(nameof(Home.InsuranceType))]
    [MapperIgnoreTarget(nameof(Home.InsurancePolicyNumber))]
    [MapperIgnoreTarget(nameof(Home.InsuranceAgentName))]
    [MapperIgnoreTarget(nameof(Home.InsuranceAgentPhone))]
    [MapperIgnoreTarget(nameof(Home.InsuranceAgentEmail))]
    [MapperIgnoreTarget(nameof(Home.MortgageInfo))]
    [MapperIgnoreTarget(nameof(Home.PropertyTaxAccountNumber))]
    [MapperIgnoreTarget(nameof(Home.EscrowDetails))]
    [MapperIgnoreTarget(nameof(Home.AppraisalValue))]
    [MapperIgnoreTarget(nameof(Home.AppraisalDate))]
    [MapperIgnoreTarget(nameof(Home.HeatingType))]
    [MapperIgnoreTarget(nameof(Home.AcType))]
    [MapperIgnoreTarget(nameof(Home.WaterHeaterType))]
    [MapperIgnoreTarget(nameof(Home.WaterHeaterSize))]
    public static partial Home FromSetupRequest(HomeSetupRequest source);

    [MapperIgnoreTarget(nameof(Home.Id))]
    [MapperIgnoreTarget(nameof(Home.TenantId))]
    [MapperIgnoreTarget(nameof(Home.CreatedAt))]
    [MapperIgnoreTarget(nameof(Home.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Home.IsSetupComplete))]
    [MapperIgnoreTarget(nameof(Home.Utilities))]
    [MapperIgnoreTarget(nameof(Home.PropertyLinks))]
    // Added to the request after mobile builds that send the whole home without them shipped;
    // ApplySystemFields sets them only when sent, so those builds cannot blank them.
    [MapperIgnoreTarget(nameof(Home.HeatingType))]
    [MapperIgnoreTarget(nameof(Home.AcType))]
    [MapperIgnoreTarget(nameof(Home.WaterHeaterType))]
    [MapperIgnoreTarget(nameof(Home.WaterHeaterSize))]
    public static partial void Update(UpdateHomeRequest source, Home target);

    /// <summary>
    /// Applies the heating, AC and water-heater fields from an update. A null leaves the stored
    /// value alone, since clients written before these fields existed omit them; an empty string
    /// clears it.
    /// </summary>
    public static void ApplySystemFields(UpdateHomeRequest source, Home target)
    {
        if (source.HeatingType != null) target.HeatingType = Blank(source.HeatingType);
        if (source.AcType != null) target.AcType = Blank(source.AcType);
        if (source.WaterHeaterType != null) target.WaterHeaterType = Blank(source.WaterHeaterType);
        if (source.WaterHeaterSize != null) target.WaterHeaterSize = Blank(source.WaterHeaterSize);

        static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static partial HomeUtilityDto ToUtilityDto(HomeUtility source);

    [MapperIgnoreTarget(nameof(HomeUtility.Id))]
    [MapperIgnoreTarget(nameof(HomeUtility.TenantId))]
    [MapperIgnoreTarget(nameof(HomeUtility.HomeId))]
    [MapperIgnoreTarget(nameof(HomeUtility.CreatedAt))]
    [MapperIgnoreTarget(nameof(HomeUtility.UpdatedAt))]
    [MapperIgnoreTarget(nameof(HomeUtility.Home))]
    public static partial HomeUtility FromCreateUtilityRequest(CreateHomeUtilityRequest source);

    [MapperIgnoreTarget(nameof(HomeUtility.Id))]
    [MapperIgnoreTarget(nameof(HomeUtility.TenantId))]
    [MapperIgnoreTarget(nameof(HomeUtility.HomeId))]
    [MapperIgnoreTarget(nameof(HomeUtility.UtilityType))]
    [MapperIgnoreTarget(nameof(HomeUtility.CreatedAt))]
    [MapperIgnoreTarget(nameof(HomeUtility.UpdatedAt))]
    [MapperIgnoreTarget(nameof(HomeUtility.Home))]
    public static partial void UpdateUtility(UpdateHomeUtilityRequest source, HomeUtility target);
}
