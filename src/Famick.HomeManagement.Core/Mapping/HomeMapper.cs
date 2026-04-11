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
    public static partial Home FromSetupRequest(HomeSetupRequest source);

    [MapperIgnoreTarget(nameof(Home.Id))]
    [MapperIgnoreTarget(nameof(Home.TenantId))]
    [MapperIgnoreTarget(nameof(Home.CreatedAt))]
    [MapperIgnoreTarget(nameof(Home.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Home.IsSetupComplete))]
    [MapperIgnoreTarget(nameof(Home.Utilities))]
    [MapperIgnoreTarget(nameof(Home.PropertyLinks))]
    public static partial void Update(UpdateHomeRequest source, Home target);

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
