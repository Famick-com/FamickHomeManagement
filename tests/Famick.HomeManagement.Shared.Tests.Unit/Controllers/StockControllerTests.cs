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
    public async Task ConsumeStock_WithValidRequest_ReturnsNoContent()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        var request = new ConsumeStockRequest { Amount = 1.0m };

        _mockStockService
            .Setup(s => s.ConsumeStockAsync(stockEntryId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ConsumeStock(stockEntryId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ConsumeStock_WithSpoiledFlag_ReturnsNoContent()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        var request = new ConsumeStockRequest { Amount = 1.0m, Spoiled = true };

        _mockStockService
            .Setup(s => s.ConsumeStockAsync(stockEntryId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ConsumeStock(stockEntryId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockStockService.Verify(
            s => s.ConsumeStockAsync(stockEntryId, It.Is<ConsumeStockRequest>(r => r.Spoiled == true), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeStock_WithEntryNotFound_ReturnsNotFound()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        var request = new ConsumeStockRequest { Amount = 1.0m };

        _mockStockService
            .Setup(s => s.ConsumeStockAsync(stockEntryId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("StockEntry", stockEntryId));

        // Act
        var result = await _controller.ConsumeStock(stockEntryId, request, CancellationToken.None);

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
            .Setup(s => s.ConsumeStockAsync(stockEntryId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientStockException(productId, required: 10.0m, available: 5.0m));

        // Act
        var result = await _controller.ConsumeStock(stockEntryId, request, CancellationToken.None);

        // Assert - ErrorResponse returns ObjectResult with 400 status code
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
        objectResult.Value.Should().NotBeNull();
        objectResult.Value!.ToString().Should().Contain("Insufficient stock");
    }

    #endregion

    #region QuickConsume Tests

    [Fact]
    public async Task QuickConsume_WithValidRequest_ReturnsNoContent()
    {
        // Arrange
        var request = new QuickConsumeRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 1.0m,
            ConsumeAll = false
        };

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.QuickConsume(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task QuickConsume_WithConsumeAllTrue_ReturnsNoContent()
    {
        // Arrange
        var request = new QuickConsumeRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 0,
            ConsumeAll = true
        };

        _mockStockService
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.QuickConsume(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockStockService.Verify(
            s => s.QuickConsumeAsync(It.Is<QuickConsumeRequest>(r => r.ConsumeAll == true), It.IsAny<CancellationToken>()),
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
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("Product", productId));

        // Act
        var result = await _controller.QuickConsume(request, CancellationToken.None);

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
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientStockException(productId, required: 100.0m, available: 5.0m));

        // Act
        var result = await _controller.QuickConsume(request, CancellationToken.None);

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
            .Setup(s => s.QuickConsumeAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("StockEntry", productId));

        // Act
        var result = await _controller.QuickConsume(request, CancellationToken.None);

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

    #region Helper Methods

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
