using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Controllers;

public class AddressControllerAutocompleteTests
{
    private readonly Mock<IAddressNormalizationService> _normalization = new();
    private readonly Mock<IAddressService> _addresses = new();
    private readonly Mock<IValidator<NormalizeAddressRequest>> _normalizeValidator = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<ILogger<AddressController>> _logger = new();

    private AddressController CreateController()
    {
        _normalizeValidator
            .Setup(v => v.ValidateAsync(It.IsAny<NormalizeAddressRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var controller = new AddressController(
            _normalization.Object,
            _addresses.Object,
            _normalizeValidator.Object,
            _tenantProvider.Object,
            _logger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public async Task Autocomplete_ReturnsEmpty_WhenQueryTooShort()
    {
        var controller = CreateController();

        var result = await controller.Autocomplete(query: "a", limit: 10);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<List<AddressSuggestionDto>>().Which.Should().BeEmpty();
        _addresses.Verify(a => a.AutocompleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Autocomplete_ForwardsToService_AndClampsLimit()
    {
        _addresses.Setup(a => a.AutocompleteAsync("main", 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AddressSuggestionDto>
            {
                new() { SuggestionId = Guid.NewGuid(), Source = "Local", AddressLine1 = "123 Main St" }
            });
        var controller = CreateController();

        var result = await controller.Autocomplete("main", limit: 99);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<List<AddressSuggestionDto>>().Which.Should().HaveCount(1);
        _addresses.Verify(a => a.AutocompleteAsync("main", 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveSuggestion_Returns400_WhenRequestInvalid()
    {
        var controller = CreateController();

        var result = await controller.ResolveSuggestion(
            new ResolveAddressSuggestionRequest { SuggestionId = Guid.Empty });

        var err = result.Should().BeOfType<ObjectResult>().Subject;
        err.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ResolveSuggestion_Returns410_WhenServiceReturnsNull()
    {
        _addresses.Setup(a => a.ResolveSuggestionAsync(It.IsAny<ResolveAddressSuggestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AddressDto?)null);
        var controller = CreateController();

        var result = await controller.ResolveSuggestion(
            new ResolveAddressSuggestionRequest { SuggestionId = Guid.NewGuid() });

        var err = result.Should().BeOfType<ObjectResult>().Subject;
        err.StatusCode.Should().Be(410);
    }

    [Fact]
    public async Task ResolveSuggestion_Returns200_WithAddress()
    {
        var dto = new AddressDto { Id = Guid.NewGuid(), AddressLine1 = "123 Main St" };
        _addresses.Setup(a => a.ResolveSuggestionAsync(It.IsAny<ResolveAddressSuggestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = CreateController();

        var result = await controller.ResolveSuggestion(
            new ResolveAddressSuggestionRequest { SuggestionId = Guid.NewGuid() });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(dto);
    }

    [Fact]
    public async Task StandardizeManual_Returns400_WhenNothingSupplied()
    {
        var controller = CreateController();

        var result = await controller.StandardizeManual(new StandardizeAddressRequest());

        var err = result.Should().BeOfType<ObjectResult>().Subject;
        err.StatusCode.Should().Be(400);
        _addresses.Verify(a => a.StandardizeAndCreateAsync(It.IsAny<StandardizeAddressRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StandardizeManual_Returns200_WithAddress()
    {
        var dto = new AddressDto { Id = Guid.NewGuid(), AddressLine1 = "123 Main St" };
        _addresses.Setup(a => a.StandardizeAndCreateAsync(It.IsAny<StandardizeAddressRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = CreateController();

        var result = await controller.StandardizeManual(new StandardizeAddressRequest
        {
            AddressLine1 = "123 Main St",
            City = "Springfield"
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(dto);
    }

    [Fact]
    public async Task Secondaries_Returns400_WhenIdEmpty()
    {
        var controller = CreateController();

        var result = await controller.Secondaries(Guid.Empty);

        var err = result.Should().BeOfType<ObjectResult>().Subject;
        err.StatusCode.Should().Be(400);
        _addresses.Verify(
            a => a.ExpandSuggestionSecondariesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Secondaries_Returns410_WhenServiceReturnsNull()
    {
        _addresses.Setup(a => a.ExpandSuggestionSecondariesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<AddressSuggestionDto>?)null);
        var controller = CreateController();

        var result = await controller.Secondaries(Guid.NewGuid());

        var err = result.Should().BeOfType<ObjectResult>().Subject;
        err.StatusCode.Should().Be(410);
    }

    [Fact]
    public async Task Secondaries_Returns200_WithChildList()
    {
        var children = new List<AddressSuggestionDto>
        {
            new() { SuggestionId = Guid.NewGuid(), AddressLine1 = "100 Tower Pl", AddressLine2 = "APT 1" },
            new() { SuggestionId = Guid.NewGuid(), AddressLine1 = "100 Tower Pl", AddressLine2 = "APT 2" }
        };
        _addresses.Setup(a => a.ExpandSuggestionSecondariesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(children);
        var controller = CreateController();

        var result = await controller.Secondaries(Guid.NewGuid());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<List<AddressSuggestionDto>>().Which.Should().HaveCount(2);
    }
}
