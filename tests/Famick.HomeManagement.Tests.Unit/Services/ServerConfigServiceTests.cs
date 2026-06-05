using Famick.HomeManagement.Core.DTOs.Server;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class ServerConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly ServerConfigService _service;

    public ServerConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "famick-server-config-tests-" + Guid.NewGuid());
        _configPath = Path.Combine(_tempDir, "config", "server-config.json");
        _service = new ServerConfigService(_configPath, NullLogger<ServerConfigService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_WhenFileMissing_ReturnsDefaults()
    {
        var result = await _service.GetAsync();

        result.Should().NotBeNull();
        result.Server.SetupComplete.Should().BeFalse();
        result.Server.PublicHostName.Should().Be("https://localhost");
        result.Server.TimeZone.Should().Be("UTC");
        result.EmailSettings.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsAllFields()
    {
        var dto = new ServerConfigDto
        {
            Server = new ServerSection
            {
                SetupComplete = true,
                PublicHostName = "https://home.example.com",
                TimeZone = "America/New_York",
            },
            EmailSettings = new ServerEmailSection
            {
                SmtpHost = "smtp.example.com",
                SmtpPort = 587,
            },
        };

        await _service.UpdateAsync(dto);
        var roundTrip = await _service.GetAsync();

        roundTrip.Server.SetupComplete.Should().BeTrue();
        roundTrip.Server.PublicHostName.Should().Be("https://home.example.com");
        roundTrip.Server.TimeZone.Should().Be("America/New_York");
        roundTrip.EmailSettings.Should().NotBeNull();
        roundTrip.EmailSettings!.SmtpHost.Should().Be("smtp.example.com");
        roundTrip.EmailSettings.SmtpPort.Should().Be(587);
    }

    [Fact]
    public async Task UpdateAsync_CreatesParentDirectoryWhenMissing()
    {
        Directory.Exists(Path.GetDirectoryName(_configPath)).Should().BeFalse(
            "test starts with no config directory");

        await _service.UpdateAsync(new ServerConfigDto());

        File.Exists(_configPath).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_DoesNotLeaveTempFileOnSuccess()
    {
        await _service.UpdateAsync(new ServerConfigDto());

        File.Exists(_configPath + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task SetSetupCompleteAsync_PreservesOtherFields()
    {
        var initial = new ServerConfigDto
        {
            Server = new ServerSection
            {
                SetupComplete = false,
                PublicHostName = "https://home.example.com",
                TimeZone = "America/Chicago",
            },
            EmailSettings = new ServerEmailSection { SmtpHost = "smtp.example.com" },
        };
        await _service.UpdateAsync(initial);

        await _service.SetSetupCompleteAsync(true);
        var after = await _service.GetAsync();

        after.Server.SetupComplete.Should().BeTrue();
        after.Server.PublicHostName.Should().Be("https://home.example.com");
        after.Server.TimeZone.Should().Be("America/Chicago");
        after.EmailSettings!.SmtpHost.Should().Be("smtp.example.com");
    }
}
