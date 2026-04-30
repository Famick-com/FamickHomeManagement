using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Controllers;

public class AdminAddressControllerTests
{
    private readonly Mock<IAddressService> _addresses = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<ILogger<AdminAddressController>> _logger = new();

    private AdminAddressController CreateController()
    {
        var controller = new AdminAddressController(
            _addresses.Object, _tenantProvider.Object, _logger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public async Task Rehash_DefaultsBatchSizeAndContinueToken_WhenRequestNull()
    {
        _addresses.Setup(a => a.RehashAddressesAsync(500, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RehashAddressesResult(0, null, false));
        var controller = CreateController();

        var result = await controller.Rehash(null);

        result.Should().BeOfType<OkObjectResult>();
        _addresses.Verify(a => a.RehashAddressesAsync(500, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rehash_ForwardsBatchSizeAndContinueToken()
    {
        var token = Guid.NewGuid();
        _addresses.Setup(a => a.RehashAddressesAsync(250, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RehashAddressesResult(250, Guid.NewGuid(), true));
        var controller = CreateController();

        var result = await controller.Rehash(new RehashAddressesRequest
        {
            BatchSize = 250,
            ContinueToken = token
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<RehashAddressesResult>()
            .Which.HasMore.Should().BeTrue();
        _addresses.Verify(a => a.RehashAddressesAsync(250, token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rehash_NormalizesNonPositiveBatchSizeToDefault()
    {
        _addresses.Setup(a => a.RehashAddressesAsync(500, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RehashAddressesResult(0, null, false));
        var controller = CreateController();

        await controller.Rehash(new RehashAddressesRequest { BatchSize = 0 });

        _addresses.Verify(a => a.RehashAddressesAsync(500, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
