using System.Text.Json;
using System.Text.Json.Nodes;
using Famick.HomeManagement.Core.DTOs.Plugins;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Plugin.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class PluginConfigServiceTests : IDisposable
{
    private readonly string _pluginsDir;
    private readonly string _configPath;

    public PluginConfigServiceTests()
    {
        _pluginsDir = Path.Combine(Path.GetTempPath(), "famick-plugin-config-tests-" + Guid.NewGuid());
        _configPath = Path.Combine(_pluginsDir, "config.json");
        Directory.CreateDirectory(_pluginsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_pluginsDir))
        {
            Directory.Delete(_pluginsDir, recursive: true);
        }
    }

    private PluginConfigService MakeService(params (string Id, string DisplayName)[] builtins)
    {
        var plugins = builtins.Select(b =>
        {
            var mock = new Mock<IPlugin>();
            mock.SetupGet(p => p.PluginId).Returns(b.Id);
            mock.SetupGet(p => p.DisplayName).Returns(b.DisplayName);
            mock.SetupGet(p => p.Version).Returns("1.0.0");
            mock.SetupGet(p => p.IsAvailable).Returns(true);
            return mock.Object;
        }).ToArray();

        return new PluginConfigService(_pluginsDir, plugins, NullLogger<PluginConfigService>.Instance);
    }

    private async Task WriteRawConfigAsync(string json)
    {
        await File.WriteAllTextAsync(_configPath, json);
    }

    [Fact]
    public async Task GetAsync_WhenNoConfigFile_ListsAllBuiltinsAsBuiltinSource()
    {
        var service = MakeService(("usda", "USDA"), ("openfoodfacts", "Open Food Facts"));

        var result = await service.GetAsync();

        result.Plugins.Should().HaveCount(2);
        result.Plugins.Should().AllSatisfy(p =>
        {
            p.Source.Should().Be(PluginSource.Builtin);
            p.Builtin.Should().BeTrue();
            p.Enabled.Should().BeTrue();
        });
        result.Plugins.Select(p => p.Id).Should().BeEquivalentTo(new[] { "usda", "openfoodfacts" });
    }

    [Fact]
    public async Task GetAsync_WithConfiguredKrogerEntry_MasksClientSecret()
    {
        await WriteRawConfigAsync("""
            {
              "plugins": [
                {
                  "id": "kroger",
                  "enabled": true,
                  "builtin": false,
                  "type": "Famick.HomeManagement.Plugin.Kroger.KrogerStorePlugin, Famick.HomeManagement.Plugin.Kroger",
                  "displayName": "Kroger",
                  "config": {
                    "clientId": "famick-id",
                    "clientSecret": "shhh-secret"
                  }
                }
              ]
            }
            """);

        var service = MakeService();
        var result = await service.GetAsync();

        var kroger = result.Plugins.Should().ContainSingle(p => p.Id == "kroger").Subject;
        kroger.Source.Should().Be(PluginSource.Configured);
        var config = JsonNode.Parse(kroger.ConfigJson!)!.AsObject();
        config["clientId"]!.GetValue<string>().Should().Be("famick-id");
        config["clientSecret"]!.GetValue<string>().Should().Be("***");
    }

    [Fact]
    public async Task UpsertAsync_WhenConfigJsonContainsAsterisks_PreservesOnDiskSecret()
    {
        await WriteRawConfigAsync("""
            {
              "plugins": [
                {
                  "id": "kroger",
                  "enabled": true,
                  "builtin": false,
                  "displayName": "Kroger",
                  "config": { "clientId": "old-id", "clientSecret": "REAL-SECRET" }
                }
              ]
            }
            """);
        var service = MakeService();

        await service.UpsertAsync("kroger", new PluginConfigEntryDto
        {
            Id = "kroger",
            DisplayName = "Kroger",
            Enabled = false,
            Builtin = false,
            ConfigJson = """{ "clientId": "new-id", "clientSecret": "***" }""",
        });

        var raw = JsonNode.Parse(await File.ReadAllTextAsync(_configPath))!;
        var entry = raw["plugins"]!.AsArray().Single()!;
        entry["enabled"]!.GetValue<bool>().Should().BeFalse();
        entry["config"]!["clientId"]!.GetValue<string>().Should().Be("new-id");
        entry["config"]!["clientSecret"]!.GetValue<string>().Should().Be("REAL-SECRET");
    }

    [Fact]
    public async Task UpsertAsync_RoundTripsAllPublicFields()
    {
        var service = MakeService();

        await service.UpsertAsync("kroger", new PluginConfigEntryDto
        {
            Id = "kroger",
            DisplayName = "Kroger Family of Stores",
            Enabled = true,
            Builtin = false,
            Type = "Some.Type, Some.Asm",
            Assembly = "Some.Asm.dll",
            ConfigJson = """{ "clientId": "abc" }""",
        });

        var raw = JsonNode.Parse(await File.ReadAllTextAsync(_configPath))!;
        var entry = raw["plugins"]!.AsArray().Single()!;
        entry["id"]!.GetValue<string>().Should().Be("kroger");
        entry["displayName"]!.GetValue<string>().Should().Be("Kroger Family of Stores");
        entry["enabled"]!.GetValue<bool>().Should().BeTrue();
        entry["builtin"]!.GetValue<bool>().Should().BeFalse();
        entry["type"]!.GetValue<string>().Should().Be("Some.Type, Some.Asm");
        entry["assembly"]!.GetValue<string>().Should().Be("Some.Asm.dll");
        entry["config"]!["clientId"]!.GetValue<string>().Should().Be("abc");
    }

    [Fact]
    public async Task UpsertAsync_DoesNotLeaveTempFileOnSuccess()
    {
        var service = MakeService();

        await service.UpsertAsync("kroger", new PluginConfigEntryDto
        {
            Id = "kroger",
            DisplayName = "Kroger",
        });

        File.Exists(_configPath + ".tmp").Should().BeFalse();
        File.Exists(_configPath).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_RejectsBuiltinPluginId()
    {
        var service = MakeService(("usda", "USDA"));

        var act = async () => await service.DeleteAsync("usda");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*built-in*");
    }

    [Fact]
    public async Task DeleteAsync_RemovesExternalEntry()
    {
        await WriteRawConfigAsync("""
            { "plugins": [ { "id": "kroger", "enabled": true, "builtin": false, "displayName": "Kroger" } ] }
            """);
        var service = MakeService();

        await service.DeleteAsync("kroger");

        var raw = JsonNode.Parse(await File.ReadAllTextAsync(_configPath))!;
        raw["plugins"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_DiscoversDllFilesNotReferencedByConfig()
    {
        await WriteRawConfigAsync("""
            { "plugins": [ { "id": "kroger", "enabled": true, "builtin": false, "displayName": "Kroger", "assembly": "Famick.HomeManagement.Plugin.Kroger.dll" } ] }
            """);
        // The Kroger DLL is referenced — should NOT appear in discovered.
        await File.WriteAllBytesAsync(Path.Combine(_pluginsDir, "Famick.HomeManagement.Plugin.Kroger.dll"), new byte[] { 0x4D, 0x5A });
        // A brand-new drop-in — should appear.
        await File.WriteAllBytesAsync(Path.Combine(_pluginsDir, "SomeNewPlugin.dll"), new byte[] { 0x4D, 0x5A });

        var service = MakeService();
        var result = await service.GetAsync();

        result.Discovered.Should().ContainSingle(d => d.FileName == "SomeNewPlugin.dll");
        result.Discovered.Should().NotContain(d => d.FileName == "Famick.HomeManagement.Plugin.Kroger.dll");
    }

    [Fact]
    public async Task GetAsync_BuiltinWithoutFileEntry_GetsDefaultHelpUrl()
    {
        var service = MakeService(("usda", "USDA"), ("kroger", "Kroger"));

        var result = await service.GetAsync();

        result.Plugins.Single(p => p.Id == "usda").HelpUrl
            .Should().Be("https://fdc.nal.usda.gov/api-key-signup.html");
        result.Plugins.Single(p => p.Id == "kroger").HelpUrl
            .Should().Be("https://developer.kroger.com/manage/apps");
    }

    [Fact]
    public async Task GetAsync_ConfigEntryHelpUrl_OverridesBuiltinDefault()
    {
        await WriteRawConfigAsync("""
            { "plugins": [ { "id": "usda", "enabled": true, "builtin": true, "displayName": "USDA", "helpUrl": "https://internal.example.com/usda" } ] }
            """);
        var service = MakeService(("usda", "USDA"));

        var result = await service.GetAsync();

        result.Plugins.Single(p => p.Id == "usda").HelpUrl
            .Should().Be("https://internal.example.com/usda");
    }

    [Fact]
    public async Task UpsertAsync_PersistsHelpUrl()
    {
        var service = MakeService();

        await service.UpsertAsync("kroger", new PluginConfigEntryDto
        {
            Id = "kroger",
            DisplayName = "Kroger",
            Enabled = true,
            HelpUrl = "https://internal.example.com/kroger",
        });

        var raw = JsonNode.Parse(await File.ReadAllTextAsync(_configPath))!;
        var entry = raw["plugins"]!.AsArray().Single()!;
        entry["helpUrl"]!.GetValue<string>().Should().Be("https://internal.example.com/kroger");
    }

    [Fact]
    public async Task RegisterDiscoveredAsync_AppendsStubEntry()
    {
        var service = MakeService();

        await service.RegisterDiscoveredAsync(
            id: "newplugin",
            assemblyPath: "NewPlugin.dll",
            typeFullName: "NewPlugin.Class, NewPlugin",
            displayName: "New Plugin");

        var raw = JsonNode.Parse(await File.ReadAllTextAsync(_configPath))!;
        var entry = raw["plugins"]!.AsArray().Single()!;
        entry["id"]!.GetValue<string>().Should().Be("newplugin");
        entry["enabled"]!.GetValue<bool>().Should().BeFalse();
        entry["type"]!.GetValue<string>().Should().Be("NewPlugin.Class, NewPlugin");
        entry["assembly"]!.GetValue<string>().Should().Be("NewPlugin.dll");
    }
}
