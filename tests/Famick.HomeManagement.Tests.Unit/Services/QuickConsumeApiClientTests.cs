using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Unit tests for Quick Consume API client patterns.
/// Tests the HTTP request/response handling logic used in ShoppingApiClient.
///
/// Note: These tests validate the API client patterns without directly referencing
/// the MAUI Mobile project. The patterns mirror ShoppingApiClient implementation.
/// </summary>
public class QuickConsumeApiClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;

    public QuickConsumeApiClientTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("https://api.test.com/")
        };
    }

    #region GetProductByBarcodeAsync Pattern Tests

    [Fact]
    public async Task GetProductByBarcode_WithValidBarcode_ReturnsProduct()
    {
        // Arrange
        var barcode = "123456789012";
        var expectedProduct = new TestProductDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            LocationName = "Pantry",
            TotalStockAmount = 5.0m
        };

        SetupHttpResponse(HttpStatusCode.OK, expectedProduct);

        // Act
        var result = await GetProductByBarcodeAsync(barcode);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Test Product");
        result.Data.TotalStockAmount.Should().Be(5.0m);
    }

    [Fact]
    public async Task GetProductByBarcode_WithNotFoundBarcode_ReturnsFailure()
    {
        // Arrange
        var barcode = "999999999999";
        SetupHttpResponse(HttpStatusCode.NotFound, new { message = "Not found" });

        // Act
        var result = await GetProductByBarcodeAsync(barcode);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task GetProductByBarcode_WithServerError_ReturnsFailure()
    {
        // Arrange
        var barcode = "123456789012";
        SetupHttpResponse(HttpStatusCode.InternalServerError, new { message = "Server error" });

        // Act
        var result = await GetProductByBarcodeAsync(barcode);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetProductByBarcode_UrlEncodesBarcode()
    {
        // Arrange
        var barcodeWithSpecialChars = "123/456+789";
        SetupHttpResponse(HttpStatusCode.OK, new TestProductDto { Id = Guid.NewGuid(), Name = "Test" });

        // Act
        await GetProductByBarcodeAsync(barcodeWithSpecialChars);

        // Assert - verify the URL was properly encoded
        VerifyHttpRequestMade(HttpMethod.Get, uri =>
            uri.ToString().Contains(Uri.EscapeDataString(barcodeWithSpecialChars)));
    }

    #endregion

    #region GetStockByProductAsync Pattern Tests

    [Fact]
    public async Task GetStockByProduct_WithValidProductId_ReturnsStockEntries()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var expectedEntries = new List<TestStockEntryDto>
        {
            new() { Id = Guid.NewGuid(), ProductId = productId, Amount = 2.0m, BestBeforeDate = DateTime.UtcNow.AddDays(5) },
            new() { Id = Guid.NewGuid(), ProductId = productId, Amount = 3.0m, BestBeforeDate = DateTime.UtcNow.AddDays(10) }
        };

        SetupHttpResponse(HttpStatusCode.OK, expectedEntries);

        // Act
        var result = await GetStockByProductAsync(productId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStockByProduct_WithNoStock_ReturnsEmptyList()
    {
        // Arrange
        var productId = Guid.NewGuid();
        SetupHttpResponse(HttpStatusCode.OK, new List<TestStockEntryDto>());

        // Act
        var result = await GetStockByProductAsync(productId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region QuickConsumeAsync Pattern Tests

    [Fact]
    public async Task QuickConsume_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new TestQuickConsumeRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 1.0m,
            ConsumeAll = false
        };
        SetupHttpResponse(HttpStatusCode.NoContent, (object?)null);

        // Act
        var result = await QuickConsumeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task QuickConsume_WithConsumeAll_ReturnsSuccess()
    {
        // Arrange
        var request = new TestQuickConsumeRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 0,
            ConsumeAll = true
        };
        SetupHttpResponse(HttpStatusCode.NoContent, (object?)null);

        // Act
        var result = await QuickConsumeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task QuickConsume_WithInsufficientStock_ReturnsFailure()
    {
        // Arrange
        var request = new TestQuickConsumeRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 100.0m
        };
        SetupHttpResponse(HttpStatusCode.BadRequest, new { message = "Insufficient stock. Required: 100, Available: 5" });

        // Act
        var result = await QuickConsumeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Insufficient stock");
    }

    [Fact]
    public async Task QuickConsume_WithProductNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new TestQuickConsumeRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 1.0m
        };
        SetupHttpResponse(HttpStatusCode.NotFound, new { message = "Product not found" });

        // Act
        var result = await QuickConsumeAsync(request);

        // Assert
        result.Success.Should().BeFalse();
    }

    #endregion

    #region ConsumeStockEntryAsync Pattern Tests

    [Fact]
    public async Task ConsumeStockEntry_WithValidEntry_ReturnsSuccess()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        SetupHttpResponse(HttpStatusCode.NoContent, (object?)null);

        // Act
        var result = await ConsumeStockEntryAsync(stockEntryId, 1.0m);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ConsumeStockEntry_WithSpoiledFlag_ReturnsSuccess()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        SetupHttpResponse(HttpStatusCode.NoContent, (object?)null);

        // Act
        var result = await ConsumeStockEntryAsync(stockEntryId, 1.0m, spoiled: true);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ConsumeStockEntry_WithEntryNotFound_ReturnsFailure()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        SetupHttpResponse(HttpStatusCode.NotFound, new { message = "Stock entry not found" });

        // Act
        var result = await ConsumeStockEntryAsync(stockEntryId, 1.0m);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeStockEntry_WithInsufficientAmount_ReturnsFailure()
    {
        // Arrange
        var stockEntryId = Guid.NewGuid();
        SetupHttpResponse(HttpStatusCode.BadRequest, new { message = "Insufficient stock. Required: 10, Available: 5" });

        // Act
        var result = await ConsumeStockEntryAsync(stockEntryId, 10.0m);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Insufficient stock");
    }

    #endregion

    #region Error Parsing Tests

    [Theory]
    [InlineData(@"{""message"":""Test error""}", "Test error")]
    [InlineData(@"{""error_message"":""Test error""}", "Test error")]
    [InlineData(@"{""errorMessage"":""Test error""}", "Test error")]
    [InlineData(@"{""error"":""Test error""}", "Test error")]
    [InlineData(@"{""title"":""Test error""}", "Test error")]
    public void ParseErrorMessage_WithVariousFormats_ExtractsMessage(string json, string expected)
    {
        // Act
        var result = ParseErrorMessage(json);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ParseErrorMessage_WithPlainText_ReturnsTruncated()
    {
        // Arrange
        var longError = new string('x', 300);

        // Act
        var result = ParseErrorMessage(longError);

        // Assert
        result.Should().HaveLength(203); // 200 + "..."
        result.Should().EndWith("...");
    }

    [Fact]
    public void ParseErrorMessage_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = ParseErrorMessage("");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods (Simulating ShoppingApiClient patterns)

    private async Task<TestApiResult<TestProductDto>> GetProductByBarcodeAsync(string barcode)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/products/by-barcode/{Uri.EscapeDataString(barcode)}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<TestProductDto>(content, options);
                return result != null
                    ? TestApiResult<TestProductDto>.Ok(result)
                    : TestApiResult<TestProductDto>.Fail("Invalid response");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return TestApiResult<TestProductDto>.Fail("Product not found in inventory");
            }

            return TestApiResult<TestProductDto>.Fail("Failed to lookup product");
        }
        catch (Exception ex)
        {
            return TestApiResult<TestProductDto>.Fail($"Connection error: {ex.Message}");
        }
    }

    private async Task<TestApiResult<List<TestStockEntryDto>>> GetStockByProductAsync(Guid productId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/stock/by-product/{productId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<List<TestStockEntryDto>>(content, options);
                return result != null
                    ? TestApiResult<List<TestStockEntryDto>>.Ok(result)
                    : TestApiResult<List<TestStockEntryDto>>.Ok(new List<TestStockEntryDto>());
            }

            return TestApiResult<List<TestStockEntryDto>>.Fail("Failed to load stock entries");
        }
        catch (Exception ex)
        {
            return TestApiResult<List<TestStockEntryDto>>.Fail($"Connection error: {ex.Message}");
        }
    }

    private async Task<TestApiResult<bool>> QuickConsumeAsync(TestQuickConsumeRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/v1/stock/quick-consume", content);

            if (response.IsSuccessStatusCode)
            {
                return TestApiResult<bool>.Ok(true);
            }

            var error = await response.Content.ReadAsStringAsync();
            return TestApiResult<bool>.Fail(ParseErrorMessage(error) ?? "Failed to consume stock");
        }
        catch (Exception ex)
        {
            return TestApiResult<bool>.Fail($"Connection error: {ex.Message}");
        }
    }

    private async Task<TestApiResult<bool>> ConsumeStockEntryAsync(Guid stockEntryId, decimal amount, bool spoiled = false)
    {
        try
        {
            var request = new { Amount = amount, Spoiled = spoiled };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"api/v1/stock/{stockEntryId}/consume", content);

            if (response.IsSuccessStatusCode)
            {
                return TestApiResult<bool>.Ok(true);
            }

            var error = await response.Content.ReadAsStringAsync();
            return TestApiResult<bool>.Fail(ParseErrorMessage(error) ?? "Failed to consume stock");
        }
        catch (Exception ex)
        {
            return TestApiResult<bool>.Fail($"Connection error: {ex.Message}");
        }
    }

    private static string? ParseErrorMessage(string errorResponse)
    {
        if (string.IsNullOrWhiteSpace(errorResponse))
            return null;

        if (!errorResponse.TrimStart().StartsWith("{"))
            return errorResponse.Length > 200 ? errorResponse[..200] + "..." : errorResponse;

        try
        {
            using var doc = JsonDocument.Parse(errorResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("error_message", out var errorMsg))
                return errorMsg.GetString();
            if (root.TryGetProperty("errorMessage", out var errorMsg2))
                return errorMsg2.GetString();
            if (root.TryGetProperty("message", out var msg))
                return msg.GetString();
            if (root.TryGetProperty("error", out var err))
                return err.GetString();
            if (root.TryGetProperty("title", out var title))
                return title.GetString();

            return errorResponse.Length > 200 ? errorResponse[..200] + "..." : errorResponse;
        }
        catch
        {
            return errorResponse.Length > 200 ? errorResponse[..200] + "..." : errorResponse;
        }
    }

    private void SetupHttpResponse<T>(HttpStatusCode statusCode, T? content)
    {
        var response = new HttpResponseMessage(statusCode);

        if (content != null)
        {
            var json = JsonSerializer.Serialize(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void VerifyHttpRequestMade(HttpMethod method, Func<Uri, bool> uriPredicate)
    {
        _mockHttpHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == method &&
                    req.RequestUri != null &&
                    uriPredicate(req.RequestUri)),
                ItExpr.IsAny<CancellationToken>());
    }

    #endregion

    #region Test DTOs

    private class TestApiResult<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }

        public static TestApiResult<T> Ok(T data) => new() { Success = true, Data = data };
        public static TestApiResult<T> Fail(string message) => new() { Success = false, ErrorMessage = message };
    }

    private class TestProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public decimal TotalStockAmount { get; set; }
    }

    private class TestStockEntryDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public decimal Amount { get; set; }
        public DateTime? BestBeforeDate { get; set; }
    }

    private class TestQuickConsumeRequest
    {
        public Guid ProductId { get; set; }
        public decimal Amount { get; set; } = 1;
        public bool ConsumeAll { get; set; }
    }

    #endregion
}
