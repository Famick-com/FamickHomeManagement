using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Plugin.Abstractions;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Which plugins each <see cref="ProductSearchMode"/> actually runs.
///
/// This had no coverage, which is how "store integrations only" came to select nothing at all:
/// it filtered lookup plugins by <c>is IStoreIntegrationPlugin</c>, and a plugin family that
/// splits lookup and store duties across two classes satisfies that on neither. The tie between
/// the halves is <see cref="IPlugin.SourceId"/>, so that is what the mode matches on.
/// </summary>
public class ProductLookupSearchModeTests
{
    // Two plugin families: one lookup-only (think USDA), one split across a lookup half and a
    // store-integration half sharing a SourceId (think Kroger after its v2.0.0 split).
    private static readonly Guid LookupOnlySource = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StoreBackedSource = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IProductLookupPlugin> _lookupOnlyPlugin = NewLookupPlugin("usda", LookupOnlySource);
    private readonly Mock<IProductLookupPlugin> _storeBackedLookupPlugin = NewLookupPlugin("kroger-lookup", StoreBackedSource);
    private readonly Mock<IStoreIntegrationPlugin> _storePlugin = NewStorePlugin("kroger", StoreBackedSource);

    private readonly Mock<IPluginLoader> _pluginLoader = new();
    private readonly Mock<ITenantService> _tenantService = new();
    private readonly Mock<IProductSearchService> _searchService = new();

    private List<string> _disabledPluginIds = new();

    public ProductLookupSearchModeTests()
    {
        _pluginLoader
            .Setup(l => l.GetAvailablePlugins<IProductLookupPlugin>())
            .Returns(() => new List<IProductLookupPlugin>
            {
                _lookupOnlyPlugin.Object,
                _storeBackedLookupPlugin.Object
            });

        _pluginLoader
            .Setup(l => l.GetAvailablePlugins<IStoreIntegrationPlugin>())
            .Returns(() => new List<IStoreIntegrationPlugin> { _storePlugin.Object });

        _tenantService
            .Setup(t => t.GetDisabledPluginIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _disabledPluginIds);

        _searchService
            .Setup(s => s.SearchLocalForLookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>());
    }

    private static Mock<IProductLookupPlugin> NewLookupPlugin(string pluginId, Guid sourceId)
    {
        var mock = new Mock<IProductLookupPlugin>();
        mock.SetupGet(p => p.PluginId).Returns(pluginId);
        mock.SetupGet(p => p.DisplayName).Returns(pluginId);
        mock.SetupGet(p => p.SourceId).Returns(sourceId);
        mock.SetupGet(p => p.Attribution).Returns((PluginAttribution?)null);
        mock.Setup(p => p.LookupAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ProductLookupLocation?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductLookupResult>());
        mock.Setup(p => p.EnrichPipelineAsync(
                It.IsAny<ProductLookupPipelineContext>(), It.IsAny<List<ProductLookupResult>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static Mock<IStoreIntegrationPlugin> NewStorePlugin(string pluginId, Guid sourceId)
    {
        var mock = new Mock<IStoreIntegrationPlugin>();
        mock.SetupGet(p => p.PluginId).Returns(pluginId);
        mock.SetupGet(p => p.SourceId).Returns(sourceId);
        return mock;
    }

    private ProductLookupService CreateService()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"lookup-mode-{Guid.NewGuid()}")
            .Options;

        return new ProductLookupService(
            _pluginLoader.Object,
            new HomeManagementDbContext(options),
            Mock.Of<ITenantProvider>(),
            _tenantService.Object,
            _searchService.Object,
            NullLogger<ProductLookupService>.Instance);
    }

    private void VerifyRan(Mock<IProductLookupPlugin> plugin, Times times) =>
        plugin.Verify(p => p.LookupAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<ProductLookupLocation?>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task StoreIntegrationsOnly_RunsTheLookupPluginBackedByAConnectedStore()
    {
        // The regression this guards: the store-backed lookup plugin is a separate class from the
        // store-integration plugin, so an interface test excludes it and the mode runs nothing.
        await CreateService().SearchAsync("milk", searchMode: ProductSearchMode.StoreIntegrationsOnly);

        VerifyRan(_storeBackedLookupPlugin, Times.Once());
        VerifyRan(_lookupOnlyPlugin, Times.Never());
    }

    [Fact]
    public async Task StoreIntegrationsOnly_WithNoStoreIntegrationLoaded_RunsNothing()
    {
        _pluginLoader
            .Setup(l => l.GetAvailablePlugins<IStoreIntegrationPlugin>())
            .Returns(new List<IStoreIntegrationPlugin>());

        await CreateService().SearchAsync("milk", searchMode: ProductSearchMode.StoreIntegrationsOnly);

        VerifyRan(_storeBackedLookupPlugin, Times.Never());
        VerifyRan(_lookupOnlyPlugin, Times.Never());
    }

    [Fact]
    public async Task StoreIntegrationsOnly_WhenTheStoreHalfIsDisabled_DropsItsLookupSibling()
    {
        // Turning the store integration off should take its store-only lookup with it — otherwise
        // this mode would still query a source the tenant has switched off.
        _disabledPluginIds = new List<string> { "kroger" };

        await CreateService().SearchAsync("milk", searchMode: ProductSearchMode.StoreIntegrationsOnly);

        VerifyRan(_storeBackedLookupPlugin, Times.Never());
    }

    [Fact]
    public async Task AllSources_RunsEveryLookupPlugin()
    {
        await CreateService().SearchAsync("milk", searchMode: ProductSearchMode.AllSources);

        VerifyRan(_lookupOnlyPlugin, Times.Once());
        VerifyRan(_storeBackedLookupPlugin, Times.Once());
    }

    [Fact]
    public async Task ExternalSourcesOnly_RunsEveryLookupPluginButSkipsTheLocalSearch()
    {
        await CreateService().SearchAsync("milk", searchMode: ProductSearchMode.ExternalSourcesOnly);

        VerifyRan(_lookupOnlyPlugin, Times.Once());
        VerifyRan(_storeBackedLookupPlugin, Times.Once());
        _searchService.Verify(
            s => s.SearchLocalForLookupAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task LocalProductsOnly_RunsNoPluginsAtAll()
    {
        await CreateService().SearchAsync("milk", searchMode: ProductSearchMode.LocalProductsOnly);

        VerifyRan(_lookupOnlyPlugin, Times.Never());
        VerifyRan(_storeBackedLookupPlugin, Times.Never());
    }

    [Fact]
    public async Task ATenantDisabledLookupPluginNeverRuns()
    {
        _disabledPluginIds = new List<string> { "usda" };

        await CreateService().SearchAsync("milk", searchMode: ProductSearchMode.AllSources);

        VerifyRan(_lookupOnlyPlugin, Times.Never());
        VerifyRan(_storeBackedLookupPlugin, Times.Once());
    }
}
