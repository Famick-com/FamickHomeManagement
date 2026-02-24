using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for ProductsController endpoints used by Quick Consume feature.
/// Focuses on barcode lookup functionality.
/// </summary>
public class ProductsControllerTests
{
    private readonly Mock<IProductsService> _mockProductsService;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly ProductsController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ProductsControllerTests()
    {
        _mockProductsService = new Mock<IProductsService>();
        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        var mockFileStorage = new Mock<IFileStorageService>();
        var mockTokenService = new Mock<IFileAccessTokenService>();
        var mockCreateValidator = new Mock<IValidator<CreateProductRequest>>();
        var mockUpdateValidator = new Mock<IValidator<UpdateProductRequest>>();
        var logger = new Mock<ILogger<ProductsController>>();

        _controller = new ProductsController(
            _mockProductsService.Object,
            mockFileStorage.Object,
            mockTokenService.Object,
            mockCreateValidator.Object,
            mockUpdateValidator.Object,
            _mockTenantProvider.Object,
            logger.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region GetByBarcode Tests

    [Fact]
    public async Task GetByBarcode_WithValidBarcode_ReturnsOkWithProduct()
    {
        // Arrange
        var barcode = "012345678905";
        var product = CreateProduct(barcode);

        _mockProductsService
            .Setup(s => s.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _controller.GetByBarcode(barcode, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedProduct = okResult.Value.Should().BeAssignableTo<ProductDto>().Subject;
        returnedProduct.Name.Should().Be("Test Product");
        returnedProduct.Barcodes.Should().Contain(b => b.Barcode == barcode);
    }

    [Fact]
    public async Task GetByBarcode_WithNotFoundBarcode_ReturnsNotFound()
    {
        // Arrange
        var barcode = "999999999999";

        _mockProductsService
            .Setup(s => s.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);

        // Act
        var result = await _controller.GetByBarcode(barcode, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetByBarcode_WithEmptyBarcode_ReturnsNotFound()
    {
        // Arrange
        var barcode = "";

        _mockProductsService
            .Setup(s => s.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);

        // Act
        var result = await _controller.GetByBarcode(barcode, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetByBarcode_VerifiesServiceCalledWithExactBarcode()
    {
        // Arrange
        var barcode = "012345678905";

        _mockProductsService
            .Setup(s => s.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);

        // Act
        await _controller.GetByBarcode(barcode, CancellationToken.None);

        // Assert
        _mockProductsService.Verify(
            s => s.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByBarcode_WithProductHavingStock_ReturnsProductWithStockInfo()
    {
        // Arrange
        var barcode = "012345678905";
        var product = CreateProduct(barcode);
        product.TotalStockAmount = 10.5m;
        product.StockByLocation = new List<ProductStockLocationDto>
        {
            new() { LocationId = Guid.NewGuid(), LocationName = "Pantry", Amount = 5.5m, EntryCount = 2 },
            new() { LocationId = Guid.NewGuid(), LocationName = "Freezer", Amount = 5.0m, EntryCount = 1 }
        };

        _mockProductsService
            .Setup(s => s.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _controller.GetByBarcode(barcode, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedProduct = okResult.Value.Should().BeAssignableTo<ProductDto>().Subject;
        returnedProduct.TotalStockAmount.Should().Be(10.5m);
        returnedProduct.StockByLocation.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByBarcode_WithProductHavingMultipleBarcodes_ReturnsAllBarcodes()
    {
        // Arrange
        var primaryBarcode = "012345678905";
        var product = CreateProduct(primaryBarcode);
        product.Barcodes.Add(new ProductBarcodeDto
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Barcode = "111111111111",
            Note = "Store brand barcode"
        });

        _mockProductsService
            .Setup(s => s.GetByBarcodeAsync(primaryBarcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _controller.GetByBarcode(primaryBarcode, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedProduct = okResult.Value.Should().BeAssignableTo<ProductDto>().Subject;
        returnedProduct.Barcodes.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("012345678905")]    // UPC-A
    [InlineData("5901234123457")]   // EAN-13
    [InlineData("90123456")]        // EAN-8
    [InlineData("ABC123")]          // Custom/internal barcode
    public async Task GetByBarcode_WithVariousBarcodeFormats_CallsService(string barcode)
    {
        // Arrange
        _mockProductsService
            .Setup(s => s.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);

        // Act
        await _controller.GetByBarcode(barcode, CancellationToken.None);

        // Assert
        _mockProductsService.Verify(
            s => s.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByBarcode_WithProductBelowMinStock_ReturnsIsBelowMinStockTrue()
    {
        // Arrange
        var barcode = "012345678905";
        var product = CreateProduct(barcode);
        product.MinStockAmount = 10m;
        product.TotalStockAmount = 3m; // Below minimum

        _mockProductsService
            .Setup(s => s.GetByBarcodeAsync(barcode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Act
        var result = await _controller.GetByBarcode(barcode, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedProduct = okResult.Value.Should().BeAssignableTo<ProductDto>().Subject;
        returnedProduct.IsBelowMinStock.Should().BeTrue();
    }

    #endregion

    #region AddImageFromUrl Tests

    [Fact]
    public async Task AddImageFromUrl_WithValidUrl_ReturnsCreated()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new AddImageFromUrlRequest { ImageUrl = "https://example.com/image.jpg" };
        var uploadedImage = new ProductImageDto
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            FileName = "image_12345.jpg",
            OriginalFileName = "image.jpg",
            ContentType = "image/jpeg",
            FileSize = 12345
        };

        _mockProductsService
            .Setup(s => s.AddImageFromUrlAsync(productId, request.ImageUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedImage);

        // Act
        var result = await _controller.AddImageFromUrl(productId, request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var returnedImage = createdResult.Value.Should().BeAssignableTo<ProductImageDto>().Subject;
        returnedImage.ProductId.Should().Be(productId);
        returnedImage.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task AddImageFromUrl_WithEmptyUrl_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new AddImageFromUrlRequest { ImageUrl = "" };

        // Act
        var result = await _controller.AddImageFromUrl(productId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddImageFromUrl_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new AddImageFromUrlRequest { ImageUrl = null! };

        // Act
        var result = await _controller.AddImageFromUrl(productId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddImageFromUrl_WithInvalidUrl_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new AddImageFromUrlRequest { ImageUrl = "not-a-valid-url" };

        // Act
        var result = await _controller.AddImageFromUrl(productId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddImageFromUrl_ProductNotFound_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new AddImageFromUrlRequest { ImageUrl = "https://example.com/image.jpg" };

        _mockProductsService
            .Setup(s => s.AddImageFromUrlAsync(productId, request.ImageUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductImageDto?)null);

        // Act
        var result = await _controller.AddImageFromUrl(productId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddImageFromUrl_ServiceCalledWithCorrectParameters()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var imageUrl = "https://example.com/product-image.png";
        var request = new AddImageFromUrlRequest { ImageUrl = imageUrl };

        _mockProductsService
            .Setup(s => s.AddImageFromUrlAsync(productId, imageUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductImageDto { Id = Guid.NewGuid(), ProductId = productId });

        // Act
        await _controller.AddImageFromUrl(productId, request, CancellationToken.None);

        // Assert
        _mockProductsService.Verify(
            s => s.AddImageFromUrlAsync(productId, imageUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static ProductDto CreateProduct(string barcode)
    {
        var productId = Guid.NewGuid();
        return new ProductDto
        {
            Id = productId,
            Name = "Test Product",
            Description = "A test product",
            LocationId = Guid.NewGuid(),
            LocationName = "Pantry",
            QuantityUnitIdPurchase = Guid.NewGuid(),
            QuantityUnitPurchaseName = "Piece",
            QuantityUnitIdStock = Guid.NewGuid(),
            QuantityUnitStockName = "Piece",
            QuantityUnitFactorPurchaseToStock = 1,
            MinStockAmount = 1,
            DefaultBestBeforeDays = 30,
            TracksBestBeforeDate = true,
            IsActive = true,
            TotalStockAmount = 5,
            Barcodes = new List<ProductBarcodeDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Barcode = barcode,
                    Note = "Primary barcode"
                }
            },
            Images = new List<ProductImageDto>(),
            StockByLocation = new List<ProductStockLocationDto>(),
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
