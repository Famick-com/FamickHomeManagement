using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for StockController endpoints used by Quick Consume feature.
/// </summary>
public class StockControllerTests
{
    private readonly Mock<IStockService> _mockStockService;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly StockController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public StockControllerTests()
    {
        _mockStockService = new Mock<IStockService>();
        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        var logger = new Mock<ILogger<StockController>>();

        _controller = new StockController(
            _mockStockService.Object,
            _mockTenantProvider.Object,
            logger.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region GetByProduct Tests

    [Fact]
    public async Task GetByProduct_WithValidProductId_ReturnsOkWithEntries()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var entries = new List<StockEntryDto>
        {
            CreateStockEntry(productId, amount: 2, daysUntilExpiry: -1), // expired
            CreateStockEntry(productId, amount: 3, daysUntilExpiry: 5),  // expires soon
            CreateStockEntry(productId, amount: 5, daysUntilExpiry: 30), // fresh
        };

        _mockStockService
            .Setup(s => s.GetByProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _controller.GetByProduct(productId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEntries = okResult.Value.Should().BeAssignableTo<List<StockEntryDto>>().Subject;
        returnedEntries.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByProduct_WithNoStock_ReturnsOkWithEmptyList()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _mockStockService
            .Setup(s => s.GetByProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockEntryDto>());

        // Act
        var result = await _controller.GetByProduct(productId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEntries = okResult.Value.Should().BeAssignableTo<List<StockEntryDto>>().Subject;
        returnedEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByProduct_VerifiesServiceCalled()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _mockStockService
            .Setup(s => s.GetByProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockEntryDto>());

        // Act
        await _controller.GetByProduct(productId, CancellationToken.None);

        // Assert
        _mockStockService.Verify(
            s => s.GetByProductAsync(productId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ConsumeStock Tests

    [Fact]
    public async Task ConsumeStock_WithValidRequest_ReturnsUpdatedRow()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        var request = new ConsumeStockRequest { Amount = 1.0m };
        var row = CreateOverviewItem(totalAmount: 4.0m);

        _mockStockService
            .Setup(s => s.ConsumeStockAsync(stockEntryId, request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        // Act
        var result = await _controller.ConsumeStock(stockEntryId, request, null, null, CancellationToken.None);

        // Assert — the caller gets the refreshed row so it can patch one row, not reload the list.
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(row);
    }

    [Fact]
    public async Task ConsumeStock_WhenProductHasNoStockLeft_ReturnsNoContent()
    {
        // Arrange — a null row means the product holds no stock, so it has no overview row.
        var stockEntryId = Guid.NewGuid();
        var request = new ConsumeStockRequest { Amount = 5.0m };

        _mockStockService
            .Setup(s => s.ConsumeStockAsync(stockEntryId, request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockOverviewItemDto?)null);

        // Act
        var result = await _controller.ConsumeStock(stockEntryId, request, null, null, CancellationToken.None);

        // Assert — 204 tells the client to drop the row.
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ConsumeStock_WithSpoiledFlag_ReturnsUpdatedRow()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        var request = new ConsumeStockRequest { Amount = 1.0m, Spoiled = true };

        _mockStockService
            .Setup(s => s.ConsumeStockAsync(stockEntryId, request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOverviewItem());

        // Act
        var result = await _controller.ConsumeStock(stockEntryId, request, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockStockService.Verify(
            s => s.ConsumeStockAsync(stockEntryId, It.Is<ConsumeStockRequest>(r => r.Spoiled == true), It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeStock_WithEntryNotFound_ReturnsNotFound()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        var request = new ConsumeStockRequest { Amount = 1.0m };

        _mockStockService
            .Setup(s => s.ConsumeStockAsync(stockEntryId, request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("StockEntry", stockEntryId));

        // Act
        var result = await _controller.ConsumeStock(stockEntryId, request, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ConsumeStock_WithInsufficientStock_ReturnsBadRequest()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new ConsumeStockRequest { Amount = 10.0m };

        _mockStockService
            .Setup(s => s.ConsumeStockAsync(stockEntryId, request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientStockException(productId, required: 10.0m, available: 5.0m));

        // Act
        var result = await _controller.ConsumeStock(stockEntryId, request, null, null, CancellationToken.None);

        // Assert - ErrorResponse returns ObjectResult with 400 status code
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
        objectResult.Value.Should().NotBeNull();
        objectResult.Value!.ToString().Should().Contain("Insufficient stock");
    }

    #endregion

    #region QuickConsume Tests

    [Fact]
    public async Task QuickConsume_WithValidRequest_ReturnsUpdatedRow()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new QuickConsumeRequest
        {
            ProductId = productId,
            Amount = 1.0m,
            ConsumeAll = false
        };
        var row = CreateOverviewItem(productId, totalAmount: 2.0m);

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        // Act
        var result = await _controller.QuickConsume(request, null, null, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(row);
    }

    [Fact]
    public async Task QuickConsume_WhenProductRunsOut_ReturnsNoContent()
    {
        // Arrange
        var request = new QuickConsumeRequest { ProductId = Guid.NewGuid(), ConsumeAll = true };

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockOverviewItemDto?)null);

        // Act
        var result = await _controller.QuickConsume(request, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task QuickConsume_ForwardsActiveOverviewFilterSoThePatchedRowMatchesTheList()
    {
        // Arrange — with a location filter on, the row must aggregate only that location's
        // entries, or the patched number disagrees with what a reload would show.
        var locationId = Guid.NewGuid();
        var productGroupId = Guid.NewGuid();
        var request = new QuickConsumeRequest { ProductId = Guid.NewGuid(), Amount = 1.0m };

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOverviewItem());

        // Act
        await _controller.QuickConsume(request, locationId, productGroupId, CancellationToken.None);

        // Assert
        _mockStockService.Verify(
            s => s.QuickConsumeAsync(
                request,
                It.Is<StockOverviewFilterRequest?>(f =>
                    f != null && f.LocationId == locationId && f.ProductGroupId == productGroupId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QuickConsume_WithNoActiveFilter_PassesNoFilter()
    {
        // Arrange
        var request = new QuickConsumeRequest { ProductId = Guid.NewGuid(), Amount = 1.0m };

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOverviewItem());

        // Act
        await _controller.QuickConsume(request, null, null, CancellationToken.None);

        // Assert
        _mockStockService.Verify(
            s => s.QuickConsumeAsync(request, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QuickConsume_WithConsumeAllTrue_CallsServiceWithConsumeAll()
    {
        // Arrange
        var request = new QuickConsumeRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 0,
            ConsumeAll = true
        };

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockOverviewItemDto?)null);

        // Act
        var result = await _controller.QuickConsume(request, null, null, CancellationToken.None);

        // Assert — consuming everything leaves no row.
        result.Should().BeOfType<NoContentResult>();
        _mockStockService.Verify(
            s => s.QuickConsumeAsync(It.Is<QuickConsumeRequest>(r => r.ConsumeAll == true), It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QuickConsume_WithProductNotFound_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new QuickConsumeRequest
        {
            ProductId = productId,
            Amount = 1.0m
        };

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("Product", productId));

        // Act
        var result = await _controller.QuickConsume(request, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task QuickConsume_WithInsufficientStock_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new QuickConsumeRequest
        {
            ProductId = productId,
            Amount = 100.0m
        };

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientStockException(productId, required: 100.0m, available: 5.0m));

        // Act
        var result = await _controller.QuickConsume(request, null, null, CancellationToken.None);

        // Assert - ErrorResponse returns ObjectResult with 400 status code
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
        objectResult.Value!.ToString().Should().Contain("Insufficient stock");
        objectResult.Value!.ToString().Should().Contain("100");
        objectResult.Value!.ToString().Should().Contain("5");
    }

    [Fact]
    public async Task QuickConsume_WithNoStockEntries_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new QuickConsumeRequest
        {
            ProductId = productId,
            Amount = 1.0m
        };

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("StockEntry", productId));

        // Act
        var result = await _controller.QuickConsume(request, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetStatistics Tests

    [Fact]
    public async Task GetStatistics_ReturnsOkWithStatistics()
    {
        // Arrange
        var statistics = new StockStatisticsDto
        {
            TotalProductCount = 50,
            ExpiredCount = 3,
            DueSoonCount = 7,
            BelowMinStockCount = 2,
            TotalStockValue = 1234.56m
        };

        _mockStockService
            .Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statistics);

        // Act
        var result = await _controller.GetStatistics(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedStats = okResult.Value.Should().BeAssignableTo<StockStatisticsDto>().Subject;
        returnedStats.TotalProductCount.Should().Be(50);
        returnedStats.ExpiredCount.Should().Be(3);
        returnedStats.DueSoonCount.Should().Be(7);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_WithValidId_ReturnsOkWithEntry()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        var entry = CreateStockEntry(Guid.NewGuid(), amount: 5, daysUntilExpiry: 10);
        entry.Id = stockEntryId;

        _mockStockService
            .Setup(s => s.GetByIdAsync(stockEntryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        // Act
        var result = await _controller.GetById(stockEntryId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedEntry = okResult.Value.Should().BeAssignableTo<StockEntryDto>().Subject;
        returnedEntry.Id.Should().Be(stockEntryId);
    }

    [Fact]
    public async Task GetById_WithNotFound_ReturnsNotFound()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();

        _mockStockService
            .Setup(s => s.GetByIdAsync(stockEntryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockEntryDto?)null);

        // Act
        var result = await _controller.GetById(stockEntryId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_WithRemainingStock_ReturnsUpdatedRow()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        var row = CreateOverviewItem();

        _mockStockService
            .Setup(s => s.DeleteAsync(stockEntryId, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        // Act
        var result = await _controller.Delete(stockEntryId, null, null, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(row);
    }

    [Fact]
    public async Task Delete_WhenItWasTheLastEntry_ReturnsNoContent()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();

        _mockStockService
            .Setup(s => s.DeleteAsync(stockEntryId, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockOverviewItemDto?)null);

        // Act
        var result = await _controller.Delete(stockEntryId, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_WithEntryNotFound_ReturnsNotFound()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();

        _mockStockService
            .Setup(s => s.DeleteAsync(stockEntryId, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("StockEntry", stockEntryId));

        // Act
        var result = await _controller.Delete(stockEntryId, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region QuickAdd Tests

    [Fact]
    public async Task QuickAdd_ReturnsUpdatedRow()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var row = CreateOverviewItem(productId, totalAmount: 4.0m);

        _mockStockService
            .Setup(s => s.QuickAddAsync(productId, 1m, null, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        // Act
        var result = await _controller.QuickAdd(productId, cancellationToken: CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(row);
    }

    [Fact]
    public async Task QuickAdd_ForwardsAmountBestBeforeDateAndActiveFilter()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var bestBefore = DateTime.UtcNow.Date.AddDays(10);

        _mockStockService
            .Setup(s => s.QuickAddAsync(productId, 3m, bestBefore, It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOverviewItem(productId));

        // Act
        await _controller.QuickAdd(productId, 3m, bestBefore, locationId, null, CancellationToken.None);

        // Assert
        _mockStockService.Verify(
            s => s.QuickAddAsync(
                productId, 3m, bestBefore,
                It.Is<StockOverviewFilterRequest?>(f => f != null && f.LocationId == locationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QuickAdd_WithProductNotFound_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _mockStockService
            .Setup(s => s.QuickAddAsync(productId, It.IsAny<decimal>(), It.IsAny<DateTime?>(), It.IsAny<StockOverviewFilterRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("Product", productId));

        // Act
        var result = await _controller.QuickAdd(productId, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Helper Methods

    private static StockOverviewItemDto CreateOverviewItem(Guid? productId = null, decimal totalAmount = 3.0m)
    {
        return new StockOverviewItemDto
        {
            ProductId = productId ?? Guid.NewGuid(),
            ProductName = "Test Product",
            TotalAmount = totalAmount,
            QuantityUnitName = "Piece",
            NextDueDate = DateTime.UtcNow.Date.AddDays(4),
            DaysUntilDue = 4,
            TotalValue = totalAmount * 2m,
            MinStockAmount = 1m,
            StockEntryCount = 1
        };
    }

    private static StockEntryDto CreateStockEntry(Guid productId, decimal amount, int? daysUntilExpiry)
    {
        return new StockEntryDto
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Test Product",
            Amount = amount,
            BestBeforeDate = daysUntilExpiry.HasValue
                ? DateTime.UtcNow.Date.AddDays(daysUntilExpiry.Value)
                : null,
            PurchasedDate = DateTime.UtcNow.AddDays(-7),
            StockId = $"STOCK-{Guid.NewGuid():N}".Substring(0, 10),
            LocationId = Guid.NewGuid(),
            LocationName = "Pantry",
            QuantityUnitName = "Piece",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
