using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Core.DTOs.StoreIntegrations;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// FHM-59: lookup-barcode used to answer every failure from the store-integration path with
/// 404 "Store integration not available" — the same answer for a throttled store, a
/// genuinely unconfigured one, and a bug in the plugin.
/// </summary>
public class ShoppingListsControllerLookupBarcodeTests
{
    private readonly Mock<IShoppingListService> _shoppingListServiceMock = new();
    private readonly Mock<IStoreIntegrationService> _storeIntegrationServiceMock = new();
    private readonly ShoppingListsController _controller;

    private readonly Guid _listId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    public ShoppingListsControllerLookupBarcodeTests()
    {
        var tenantProviderMock = new Mock<ITenantProvider>();
        tenantProviderMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        _controller = new ShoppingListsController(
            _shoppingListServiceMock.Object,
            _storeIntegrationServiceMock.Object,
            new Mock<IProductSearchService>().Object,
            new Mock<IValidator<CreateShoppingListRequest>>().Object,
            new Mock<IValidator<UpdateShoppingListRequest>>().Object,
            new Mock<IValidator<AddShoppingListItemRequest>>().Object,
            new Mock<IValidator<UpdateShoppingListItemRequest>>().Object,
            new Mock<IValidator<AddToShoppingListRequest>>().Object,
            tenantProviderMock.Object,
            new Mock<ILogger<ShoppingListsController>>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        _shoppingListServiceMock
            .Setup(s => s.GetListByIdAsync(_listId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShoppingListDto { Id = _listId, ShoppingLocationId = _locationId });
    }

    private void StoreSearchThrows(Exception ex) =>
        _storeIntegrationServiceMock
            .Setup(s => s.SearchProductsAtStoreAsync(
                _locationId, It.IsAny<StoreProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);

    private Task<IActionResult> Lookup() =>
        _controller.LookupBarcode(_listId, "0001111042566", CancellationToken.None);

    [Fact]
    public async Task Throttled_Returns429NotNotFound()
    {
        StoreSearchThrows(new StoreRateLimitException("kroger", "throttled", TimeSpan.FromSeconds(30)));

        var result = await Lookup();

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Throttled_SetsRetryAfterHeaderFromThePlugin()
    {
        StoreSearchThrows(new StoreRateLimitException("kroger", "throttled", TimeSpan.FromSeconds(30)));

        await Lookup();

        _controller.Response.Headers.RetryAfter.ToString().Should().Be("30");
    }

    [Fact]
    public async Task Throttled_RoundsAFractionalRetryAfterUpNotDown()
    {
        StoreSearchThrows(new StoreRateLimitException("kroger", "throttled", TimeSpan.FromSeconds(2.4)));

        await Lookup();

        // Rounding down would tell the caller to retry while still throttled.
        _controller.Response.Headers.RetryAfter.ToString().Should().Be("3");
    }

    [Fact]
    public async Task Throttled_WithoutRetryAfter_OmitsTheHeader()
    {
        StoreSearchThrows(new StoreRateLimitException("kroger", "throttled"));

        var result = await Lookup();

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        _controller.Response.Headers.Should().NotContainKey("Retry-After");
    }

    [Fact]
    public async Task UnconfiguredStore_StillReturns404NotAvailable()
    {
        StoreSearchThrows(new StoreIntegrationUnavailableException("Shopping location has no store integration"));

        var result = await Lookup();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PluginFault_IsNoLongerDisguisedAsAnUnconfiguredStore()
    {
        // A bare InvalidOperationException from the plugin path is a genuine fault. It used to
        // land in the same 404 as a missing integration, so real failures were invisible.
        StoreSearchThrows(new InvalidOperationException("Unexpected null in the plugin response"));

        var result = await Lookup();

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task StoreTimeout_Returns504()
    {
        StoreSearchThrows(new TimeoutException("Kroger product search did not complete within 10s."));

        var result = await Lookup();

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status504GatewayTimeout);
    }

    [Fact]
    public async Task CallerCancellation_IsNotConvertedIntoAResponse()
    {
        StoreSearchThrows(new OperationCanceledException());

        var act = Lookup;

        // Nobody is waiting for the answer; turning it into a 502 would log a fault that
        // nothing went wrong with.
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task NoMatch_StillReturns404ProductNotFound()
    {
        _storeIntegrationServiceMock
            .Setup(s => s.SearchProductsAtStoreAsync(
                _locationId, It.IsAny<StoreProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StoreProductResult>());

        var result = await Lookup();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Match_Returns200()
    {
        _storeIntegrationServiceMock
            .Setup(s => s.SearchProductsAtStoreAsync(
                _locationId, It.IsAny<StoreProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StoreProductResult { Name = "Whole Milk" }]);

        var result = await Lookup();

        result.Should().BeOfType<OkObjectResult>();
    }
}
