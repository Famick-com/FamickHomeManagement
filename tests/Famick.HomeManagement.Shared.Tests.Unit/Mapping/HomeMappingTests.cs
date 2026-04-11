using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Home;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class HomeMappingTests
{
    private readonly IMapper _mapper;

    public HomeMappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<HomeMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        // Validation skipped: profiles are tested in isolation
        _mapper = config.CreateMapper();
    }

    #region Home -> HomeDto

    [Fact]
    public void Home_To_HomeDto_MapsAllProperties()
    {
        var homeId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var home = new Home
        {
            Id = homeId,
            TenantId = Guid.NewGuid(),
            Unit = "4B",
            YearBuilt = 1995,
            SquareFootage = 2200,
            Bedrooms = 3,
            Bathrooms = 2.5m,
            HoaName = "Sunset HOA",
            HoaContactInfo = "555-1234",
            HoaRulesLink = "https://hoa.example.com/rules",
            AcFilterSizes = "20x25x1",
            AcFilterReplacementIntervalDays = 90,
            FridgeWaterFilterType = "LT700P",
            UnderSinkFilterType = "RO-500",
            WholeHouseFilterType = "Big Blue 20",
            SmokeCoDetectorBatteryType = "9V",
            HvacServiceSchedule = "Biannual",
            PestControlSchedule = "Quarterly",
            InsuranceType = InsuranceType.Homeowners,
            InsurancePolicyNumber = "POL-123456",
            InsuranceAgentName = "John Agent",
            InsuranceAgentPhone = "555-9876",
            InsuranceAgentEmail = "agent@ins.com",
            MortgageInfo = "30yr fixed 3.5%",
            PropertyTaxAccountNumber = "TAX-001",
            EscrowDetails = "Monthly escrow",
            AppraisalValue = 450000m,
            AppraisalDate = now.AddMonths(-6),
            IsSetupComplete = true,
            CreatedAt = now.AddYears(-1),
            UpdatedAt = now,
            Utilities = new List<HomeUtility>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    UtilityType = UtilityType.Electric,
                    CompanyName = "PowerCo"
                }
            }
        };

        var dto = _mapper.Map<HomeDto>(home);

        dto.Id.Should().Be(homeId);
        dto.Unit.Should().Be("4B");
        dto.YearBuilt.Should().Be(1995);
        dto.SquareFootage.Should().Be(2200);
        dto.Bedrooms.Should().Be(3);
        dto.Bathrooms.Should().Be(2.5m);
        dto.HoaName.Should().Be("Sunset HOA");
        dto.HoaContactInfo.Should().Be("555-1234");
        dto.HoaRulesLink.Should().Be("https://hoa.example.com/rules");
        dto.AcFilterSizes.Should().Be("20x25x1");
        dto.AcFilterReplacementIntervalDays.Should().Be(90);
        dto.FridgeWaterFilterType.Should().Be("LT700P");
        dto.UnderSinkFilterType.Should().Be("RO-500");
        dto.WholeHouseFilterType.Should().Be("Big Blue 20");
        dto.SmokeCoDetectorBatteryType.Should().Be("9V");
        dto.HvacServiceSchedule.Should().Be("Biannual");
        dto.PestControlSchedule.Should().Be("Quarterly");
        dto.InsuranceType.Should().Be(InsuranceType.Homeowners);
        dto.InsurancePolicyNumber.Should().Be("POL-123456");
        dto.InsuranceAgentName.Should().Be("John Agent");
        dto.InsuranceAgentPhone.Should().Be("555-9876");
        dto.InsuranceAgentEmail.Should().Be("agent@ins.com");
        dto.MortgageInfo.Should().Be("30yr fixed 3.5%");
        dto.PropertyTaxAccountNumber.Should().Be("TAX-001");
        dto.EscrowDetails.Should().Be("Monthly escrow");
        dto.AppraisalValue.Should().Be(450000m);
        dto.AppraisalDate.Should().Be(now.AddMonths(-6));
        dto.IsSetupComplete.Should().BeTrue();
        dto.CreatedAt.Should().Be(now.AddYears(-1));
        dto.UpdatedAt.Should().Be(now);
        dto.Utilities.Should().HaveCount(1);
        dto.Utilities[0].CompanyName.Should().Be("PowerCo");
    }

    [Fact]
    public void Home_To_HomeDto_EmptyUtilities_MapsEmptyList()
    {
        var home = new Home
        {
            Id = Guid.NewGuid(),
            Utilities = new List<HomeUtility>()
        };

        var dto = _mapper.Map<HomeDto>(home);

        dto.Utilities.Should().BeEmpty();
    }

    #endregion

    #region PropertyLink -> PropertyLinkDto

    [Fact]
    public void PropertyLink_To_PropertyLinkDto_MapsAllProperties()
    {
        var linkId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var link = new PropertyLink
        {
            Id = linkId,
            HomeId = homeId,
            Url = "https://county.gov/records",
            Label = "County Records",
            SortOrder = 1,
            CreatedAt = now.AddDays(-10),
            UpdatedAt = now
        };

        var dto = _mapper.Map<PropertyLinkDto>(link);

        dto.Id.Should().Be(linkId);
        dto.HomeId.Should().Be(homeId);
        dto.Url.Should().Be("https://county.gov/records");
        dto.Label.Should().Be("County Records");
        dto.SortOrder.Should().Be(1);
        dto.CreatedAt.Should().Be(now.AddDays(-10));
        dto.UpdatedAt.Should().Be(now);
    }

    #endregion

    #region HomeSetupRequest -> Home

    [Fact]
    public void HomeSetupRequest_To_Home_MapsEditableFields()
    {
        var request = new HomeSetupRequest
        {
            Unit = "2A",
            YearBuilt = 2010,
            SquareFootage = 1800,
            Bedrooms = 4,
            Bathrooms = 3.0m,
            HoaName = "Garden HOA",
            HoaContactInfo = "info@hoa.com",
            HoaRulesLink = "https://hoa.com/rules",
            AcFilterSizes = "16x20x1",
            SmokeCoDetectorBatteryType = "AA"
        };

        var entity = _mapper.Map<Home>(request);

        entity.Unit.Should().Be("2A");
        entity.YearBuilt.Should().Be(2010);
        entity.SquareFootage.Should().Be(1800);
        entity.Bedrooms.Should().Be(4);
        entity.Bathrooms.Should().Be(3.0m);
        entity.HoaName.Should().Be("Garden HOA");
        entity.HoaContactInfo.Should().Be("info@hoa.com");
        entity.HoaRulesLink.Should().Be("https://hoa.com/rules");
        entity.AcFilterSizes.Should().Be("16x20x1");
        entity.SmokeCoDetectorBatteryType.Should().Be("AA");
    }

    [Fact]
    public void HomeSetupRequest_To_Home_IgnoresSystemFields()
    {
        var request = new HomeSetupRequest
        {
            Unit = "1A"
        };

        var entity = _mapper.Map<Home>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.IsSetupComplete.Should().BeFalse();
        entity.Utilities.Should().BeEmpty();
        entity.PropertyLinks.Should().BeEmpty();
    }

    [Fact]
    public void HomeSetupRequest_To_Home_IgnoresMaintenanceFields()
    {
        var request = new HomeSetupRequest
        {
            YearBuilt = 2020
        };

        var entity = _mapper.Map<Home>(request);

        entity.AcFilterReplacementIntervalDays.Should().BeNull();
        entity.FridgeWaterFilterType.Should().BeNull();
        entity.UnderSinkFilterType.Should().BeNull();
        entity.WholeHouseFilterType.Should().BeNull();
        entity.HvacServiceSchedule.Should().BeNull();
        entity.PestControlSchedule.Should().BeNull();
    }

    [Fact]
    public void HomeSetupRequest_To_Home_IgnoresInsuranceFields()
    {
        var request = new HomeSetupRequest
        {
            YearBuilt = 2020
        };

        var entity = _mapper.Map<Home>(request);

        entity.InsuranceType.Should().BeNull();
        entity.InsurancePolicyNumber.Should().BeNull();
        entity.InsuranceAgentName.Should().BeNull();
        entity.InsuranceAgentPhone.Should().BeNull();
        entity.InsuranceAgentEmail.Should().BeNull();
        entity.MortgageInfo.Should().BeNull();
        entity.PropertyTaxAccountNumber.Should().BeNull();
        entity.EscrowDetails.Should().BeNull();
        entity.AppraisalValue.Should().BeNull();
        entity.AppraisalDate.Should().BeNull();
    }

    #endregion

    #region UpdateHomeRequest -> Home

    [Fact]
    public void UpdateHomeRequest_To_Home_MapsAllEditableFields()
    {
        var now = DateTime.UtcNow;
        var request = new UpdateHomeRequest
        {
            Unit = "5C",
            YearBuilt = 2005,
            SquareFootage = 3000,
            Bedrooms = 5,
            Bathrooms = 3.5m,
            HoaName = "Lake HOA",
            HoaContactInfo = "lake@hoa.com",
            HoaRulesLink = "https://lake-hoa.com",
            AcFilterSizes = "20x25x1, 16x20x1",
            AcFilterReplacementIntervalDays = 60,
            FridgeWaterFilterType = "DA29",
            UnderSinkFilterType = "Under-Pro",
            WholeHouseFilterType = "Whole-Pro",
            SmokeCoDetectorBatteryType = "10-year sealed",
            HvacServiceSchedule = "Annual",
            PestControlSchedule = "Monthly",
            InsuranceType = InsuranceType.Renters,
            InsurancePolicyNumber = "RNT-999",
            InsuranceAgentName = "Agent Smith",
            InsuranceAgentPhone = "555-0001",
            InsuranceAgentEmail = "smith@ins.com",
            MortgageInfo = "15yr fixed",
            PropertyTaxAccountNumber = "TAX-999",
            EscrowDetails = "Quarterly",
            AppraisalValue = 600000m,
            AppraisalDate = now
        };

        var entity = _mapper.Map<Home>(request);

        entity.Unit.Should().Be("5C");
        entity.YearBuilt.Should().Be(2005);
        entity.SquareFootage.Should().Be(3000);
        entity.Bedrooms.Should().Be(5);
        entity.Bathrooms.Should().Be(3.5m);
        entity.HoaName.Should().Be("Lake HOA");
        entity.AcFilterSizes.Should().Be("20x25x1, 16x20x1");
        entity.AcFilterReplacementIntervalDays.Should().Be(60);
        entity.FridgeWaterFilterType.Should().Be("DA29");
        entity.InsuranceType.Should().Be(InsuranceType.Renters);
        entity.InsurancePolicyNumber.Should().Be("RNT-999");
        entity.AppraisalValue.Should().Be(600000m);
        entity.AppraisalDate.Should().Be(now);
    }

    [Fact]
    public void UpdateHomeRequest_To_Home_IgnoresSystemFields()
    {
        var request = new UpdateHomeRequest
        {
            Unit = "1A"
        };

        var entity = _mapper.Map<Home>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.IsSetupComplete.Should().BeFalse();
        entity.Utilities.Should().BeEmpty();
        entity.PropertyLinks.Should().BeEmpty();
    }

    #endregion

    #region HomeUtility -> HomeUtilityDto

    [Fact]
    public void HomeUtility_To_HomeUtilityDto_MapsAllProperties()
    {
        var utilityId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var utility = new HomeUtility
        {
            Id = utilityId,
            TenantId = Guid.NewGuid(),
            HomeId = Guid.NewGuid(),
            UtilityType = UtilityType.Electric,
            CompanyName = "PowerCo",
            AccountNumber = "ACC-123",
            PhoneNumber = "555-1111",
            Website = "https://powerco.com",
            LoginEmail = "user@email.com",
            Notes = "Main electric",
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now
        };

        var dto = _mapper.Map<HomeUtilityDto>(utility);

        dto.Id.Should().Be(utilityId);
        dto.UtilityType.Should().Be(UtilityType.Electric);
        dto.CompanyName.Should().Be("PowerCo");
        dto.AccountNumber.Should().Be("ACC-123");
        dto.PhoneNumber.Should().Be("555-1111");
        dto.Website.Should().Be("https://powerco.com");
        dto.LoginEmail.Should().Be("user@email.com");
        dto.Notes.Should().Be("Main electric");
        dto.CreatedAt.Should().Be(now.AddDays(-30));
        dto.UpdatedAt.Should().Be(now);
    }

    #endregion

    #region CreateHomeUtilityRequest -> HomeUtility

    [Fact]
    public void CreateHomeUtilityRequest_To_HomeUtility_MapsEditableFields()
    {
        var request = new CreateHomeUtilityRequest
        {
            UtilityType = UtilityType.Gas,
            CompanyName = "GasCo",
            AccountNumber = "GAS-456",
            PhoneNumber = "555-2222",
            Website = "https://gasco.com",
            LoginEmail = "gas@email.com",
            Notes = "Natural gas"
        };

        var entity = _mapper.Map<HomeUtility>(request);

        entity.UtilityType.Should().Be(UtilityType.Gas);
        entity.CompanyName.Should().Be("GasCo");
        entity.AccountNumber.Should().Be("GAS-456");
        entity.PhoneNumber.Should().Be("555-2222");
        entity.Website.Should().Be("https://gasco.com");
        entity.LoginEmail.Should().Be("gas@email.com");
        entity.Notes.Should().Be("Natural gas");
    }

    [Fact]
    public void CreateHomeUtilityRequest_To_HomeUtility_IgnoresSystemFields()
    {
        var request = new CreateHomeUtilityRequest
        {
            UtilityType = UtilityType.Electric,
            CompanyName = "Test"
        };

        var entity = _mapper.Map<HomeUtility>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.HomeId.Should().Be(Guid.Empty);
    }

    #endregion

    #region UpdateHomeUtilityRequest -> HomeUtility

    [Fact]
    public void UpdateHomeUtilityRequest_To_HomeUtility_MapsEditableFields()
    {
        var request = new UpdateHomeUtilityRequest
        {
            CompanyName = "Updated Co",
            AccountNumber = "UPD-789",
            PhoneNumber = "555-3333",
            Website = "https://updated.com",
            LoginEmail = "upd@email.com",
            Notes = "Updated notes"
        };

        var entity = _mapper.Map<HomeUtility>(request);

        entity.CompanyName.Should().Be("Updated Co");
        entity.AccountNumber.Should().Be("UPD-789");
        entity.PhoneNumber.Should().Be("555-3333");
        entity.Website.Should().Be("https://updated.com");
        entity.LoginEmail.Should().Be("upd@email.com");
        entity.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public void UpdateHomeUtilityRequest_To_HomeUtility_IgnoresSystemAndTypeFields()
    {
        var request = new UpdateHomeUtilityRequest
        {
            CompanyName = "Test"
        };

        var entity = _mapper.Map<HomeUtility>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.HomeId.Should().Be(Guid.Empty);
        entity.UtilityType.Should().Be(default(UtilityType));
    }

    #endregion
}
