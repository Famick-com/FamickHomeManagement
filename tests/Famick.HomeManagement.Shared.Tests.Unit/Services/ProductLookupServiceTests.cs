using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Unit tests for ProductLookupService.
/// Tests plugin pipeline orchestration, parallelization, and search mode filtering.
/// </summary>
public class ProductLookupServiceTests
{
    private readonly Mock<IPluginLoader> _mockPluginLoader;
    private readonly Mock<IProductSearchService> _mockSearchService;
    private readonly Mock<ITenantService> _mockTenantService;
    private readonly Mock<ILogger<ProductLookupService>> _mockLogger;

    public ProductLookupServiceTests()
    {
        _mockPluginLoader = new Mock<IPluginLoader>();
        _mockSearchService = new Mock<IProductSearchService>();
        _mockTenantService = new Mock<ITenantService>();
        _mockLogger = new Mock<ILogger<ProductLookupService>>();

        // Default: no disabled plugins
        _mockTenantService
            .Setup(s => s.GetDisabledPluginIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // Default: no plugins
        _mockPluginLoader
            .Setup(l => l.GetAvailablePlugins<IProductLookupPlugin>())
            .Returns(new List<IProductLookupPlugin>());
    }

    #region Input Validation

    [Fact]
    public async Task SearchAsync_WithNullQuery_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.SearchAsync(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.SearchAsync("");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WithWhitespaceQuery_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.SearchAsync("   ");

        result.Should().BeEmpty();
    }

    #endregion

    #region Search Mode - LocalProductsOnly

    [Fact]
    public async Task SearchAsync_LocalProductsOnly_SearchesLocalAndSkipsPlugins()
    {
        // Arrange
        var localResults = new List<ProductLookupResult>
        {
            new() { Name = "Local Milk", DataSources = new() { { "Local Database", "id1" } } }
        };

        _mockSearchService
            .Setup(s => s.SearchLocalForLookupAsync("milk", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(localResults);

        var service = CreateService();

        // Act
        var results = await service.SearchAsync("milk", 20, ProductSearchMode.LocalProductsOnly);

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Local Milk");

        // Verify plugins were NOT invoked
        _mockPluginLoader.Verify(
            l => l.GetAvailablePlugins<IProductLookupPlugin>(), Times.Never);
    }

    #endregion

    #region Search Mode - ExternalSourcesOnly

    [Fact]
    public async Task SearchAsync_ExternalSourcesOnly_SkipsLocalSearch()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SearchAsync("milk", 20, ProductSearchMode.ExternalSourcesOnly);

        // Assert: Local search should NOT be called
        _mockSearchService.Verify(
            s => s.SearchLocalForLookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Parallelization - Local + Plugins Run Concurrently

    [Fact]
    public async Task SearchAsync_AllSources_RunsLocalAndPluginsInParallel()
    {
        // Arrange: Set up a plugin that returns results
        var mockPlugin = new Mock<IProductLookupPlugin>();
        mockPlugin.Setup(p => p.PluginId).Returns("test-plugin");
        mockPlugin.Setup(p => p.DisplayName).Returns("Test Plugin");
        mockPlugin
            .Setup(p => p.LookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>
            {
                new() { Name = "Plugin Result", DataSources = new() { { "test-plugin", "ext-1" } } }
            });
        mockPlugin
            .Setup(p => p.EnrichPipelineAsync(It.IsAny<ProductLookupPipelineContext>(),
                It.IsAny<List<ProductLookupResult>>(), It.IsAny<CancellationToken>()))
            .Callback<ProductLookupPipelineContext, List<ProductLookupResult>, CancellationToken>(
                (ctx, results, _) => ctx.AddResults(results))
            .Returns(Task.CompletedTask);

        _mockPluginLoader
            .Setup(l => l.GetAvailablePlugins<IProductLookupPlugin>())
            .Returns(new List<IProductLookupPlugin> { mockPlugin.Object });

        var localResults = new List<ProductLookupResult>
        {
            new() { Name = "Local Result", DataSources = new() { { "Local Database", "id1" } } }
        };
        _mockSearchService
            .Setup(s => s.SearchLocalForLookupAsync("milk", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(localResults);

        var service = CreateService();

        // Act
        var results = await service.SearchAsync("milk", 20, ProductSearchMode.AllSources);

        // Assert: Both local and plugin results should be present
        results.Should().HaveCount(2);
        results.Should().Contain(r => r.Name == "Local Result");
        results.Should().Contain(r => r.Name == "Plugin Result");

        // Verify both were called
        _mockSearchService.Verify(
            s => s.SearchLocalForLookupAsync("milk", 20, It.IsAny<CancellationToken>()),
            Times.Once);
        mockPlugin.Verify(
            p => p.LookupAsync("milk", 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_LocalResultsAppearBeforePluginResults()
    {
        // Arrange
        var mockPlugin = new Mock<IProductLookupPlugin>();
        mockPlugin.Setup(p => p.PluginId).Returns("test-plugin");
        mockPlugin
            .Setup(p => p.LookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>
            {
                new() { Name = "Plugin Result", DataSources = new() { { "test-plugin", "ext-1" } } }
            });
        mockPlugin
            .Setup(p => p.EnrichPipelineAsync(It.IsAny<ProductLookupPipelineContext>(),
                It.IsAny<List<ProductLookupResult>>(), It.IsAny<CancellationToken>()))
            .Callback<ProductLookupPipelineContext, List<ProductLookupResult>, CancellationToken>(
                (ctx, results, _) => ctx.AddResults(results))
            .Returns(Task.CompletedTask);

        _mockPluginLoader
            .Setup(l => l.GetAvailablePlugins<IProductLookupPlugin>())
            .Returns(new List<IProductLookupPlugin> { mockPlugin.Object });

        _mockSearchService
            .Setup(s => s.SearchLocalForLookupAsync("milk", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>
            {
                new() { Name = "Local Result", DataSources = new() { { "Local Database", "id1" } } }
            });

        var service = CreateService();

        // Act
        var results = await service.SearchAsync("milk", 20, ProductSearchMode.AllSources);

        // Assert: Local results should come first (added to context before enrichment)
        results[0].Name.Should().Be("Local Result");
        results[1].Name.Should().Be("Plugin Result");
    }

    #endregion

    #region Plugin Failure Handling

    [Fact]
    public async Task SearchAsync_PluginLookupFails_OtherResultsStillReturned()
    {
        // Arrange: Plugin throws during lookup
        var mockPlugin = new Mock<IProductLookupPlugin>();
        mockPlugin.Setup(p => p.PluginId).Returns("failing-plugin");
        mockPlugin
            .Setup(p => p.LookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API timeout"));

        _mockPluginLoader
            .Setup(l => l.GetAvailablePlugins<IProductLookupPlugin>())
            .Returns(new List<IProductLookupPlugin> { mockPlugin.Object });

        _mockSearchService
            .Setup(s => s.SearchLocalForLookupAsync("milk", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>
            {
                new() { Name = "Local Result", DataSources = new() { { "Local Database", "id1" } } }
            });

        var service = CreateService();

        // Act: Should not throw
        var results = await service.SearchAsync("milk", 20, ProductSearchMode.AllSources);

        // Assert: Local results should still be returned
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Local Result");
    }

    #endregion

    #region Barcode Detection

    [Theory]
    [InlineData("012345678905")]
    [InlineData("5901234123457")]
    [InlineData("90123456")]
    public async Task SearchAsync_WithBarcode_SetsSearchTypeToBarcode(string barcode)
    {
        // Arrange
        _mockSearchService
            .Setup(s => s.SearchLocalForLookupAsync(barcode, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>());

        var service = CreateService();

        // Act
        await service.SearchAsync(barcode, 20, ProductSearchMode.LocalProductsOnly);

        // Assert
        _mockSearchService.Verify(
            s => s.SearchLocalForLookupAsync(barcode, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("milk")]
    [InlineData("Organic Whole Wheat Bread")]
    [InlineData("123")] // Too short for barcode
    public async Task SearchAsync_WithText_SetsSearchTypeToName(string query)
    {
        // Arrange
        _mockSearchService
            .Setup(s => s.SearchLocalForLookupAsync(query, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>());

        var service = CreateService();

        // Act
        await service.SearchAsync(query, 20, ProductSearchMode.LocalProductsOnly);

        // Assert
        _mockSearchService.Verify(
            s => s.SearchLocalForLookupAsync(query, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Original Search Barcode

    [Fact]
    public async Task SearchAsync_BarcodeSearch_SetsOriginalSearchBarcodeOnAllResults()
    {
        // Arrange: Use AllSources mode because OriginalSearchBarcode is set
        // after the plugin pipeline, not in LocalProductsOnly early-return path
        _mockSearchService
            .Setup(s => s.SearchLocalForLookupAsync("012345678905", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>
            {
                new() { Name = "Product 1", DataSources = new() { { "Local Database", "id1" } } },
                new() { Name = "Product 2", DataSources = new() { { "Local Database", "id2" } } }
            });

        var service = CreateService();

        // Act
        var results = await service.SearchAsync("012345678905", 20, ProductSearchMode.AllSources);

        // Assert
        results.Should().AllSatisfy(r =>
            r.OriginalSearchBarcode.Should().NotBeNull());
        // BarcodeParser normalizes: strips check digit, stores 11-digit UPC-A core
        results.Should().AllSatisfy(r =>
            r.OriginalSearchBarcode!.Data.Should().Be("01234567890"));
    }

    [Fact]
    public async Task SearchAsync_NameSearch_DoesNotSetOriginalSearchBarcode()
    {
        // Arrange
        _mockSearchService
            .Setup(s => s.SearchLocalForLookupAsync("milk", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>
            {
                new() { Name = "Milk", DataSources = new() { { "Local Database", "id1" } } }
            });

        var service = CreateService();

        // Act
        var results = await service.SearchAsync("milk", 20, ProductSearchMode.AllSources);

        // Assert
        results[0].OriginalSearchBarcode.Should().BeNull();
    }

    #endregion

    #region Disabled Plugins

    [Fact]
    public async Task SearchAsync_DisabledPlugins_AreExcluded()
    {
        // Arrange
        var enabledPlugin = new Mock<IProductLookupPlugin>();
        enabledPlugin.Setup(p => p.PluginId).Returns("enabled-plugin");
        enabledPlugin
            .Setup(p => p.LookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>());
        enabledPlugin
            .Setup(p => p.EnrichPipelineAsync(It.IsAny<ProductLookupPipelineContext>(),
                It.IsAny<List<ProductLookupResult>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var disabledPlugin = new Mock<IProductLookupPlugin>();
        disabledPlugin.Setup(p => p.PluginId).Returns("disabled-plugin");

        _mockPluginLoader
            .Setup(l => l.GetAvailablePlugins<IProductLookupPlugin>())
            .Returns(new List<IProductLookupPlugin>
            {
                enabledPlugin.Object, disabledPlugin.Object
            });

        _mockTenantService
            .Setup(s => s.GetDisabledPluginIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "disabled-plugin" });

        _mockSearchService
            .Setup(s => s.SearchLocalForLookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>());

        var service = CreateService();

        // Act
        await service.SearchAsync("milk", 20, ProductSearchMode.AllSources);

        // Assert: Enabled plugin was called, disabled was not
        enabledPlugin.Verify(
            p => p.LookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        disabledPlugin.Verify(
            p => p.LookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Helper Methods

    private ProductLookupService CreateService()
    {
        return new ProductLookupService(
            _mockPluginLoader.Object,
            null!, // dbContext - not used in tested methods (ApplyLookupResultAsync uses it)
            null!, // tenantProvider - not used in SearchAsync
            _mockTenantService.Object,
            _mockSearchService.Object,
            _mockLogger.Object);
    }

    #endregion
}
