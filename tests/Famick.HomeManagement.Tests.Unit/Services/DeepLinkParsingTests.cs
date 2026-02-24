using System.Web;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Tests for deep link URL parsing logic.
/// These tests verify the URL format and parsing that would be used by the mobile app's DeepLinkHandler.
/// </summary>
public class DeepLinkParsingTests
{
    /// <summary>
    /// Simulates the parsing logic from DeepLinkHandler.
    /// </summary>
    private (string? ServerUrl, string? ServerName, bool Success) ParseSetupDeepLink(string uriString)
    {
        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
        {
            return (null, null, false);
        }

        if (!uri.Scheme.Equals("famick", StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("setup", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, false);
        }

        var query = HttpUtility.ParseQueryString(uri.Query);
        var serverUrl = query["url"];
        var serverName = query["name"];

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return (null, null, false);
        }

        // Validate the URL is well-formed
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var parsedUrl) ||
            (!parsedUrl.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
             !parsedUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
        {
            return (null, null, false);
        }

        return (serverUrl.TrimEnd('/'), serverName, true);
    }

    #region Valid Deep Link Tests

    [Fact]
    public void ParseSetupDeepLink_WithValidHttpsUrl_ReturnsServerInfo()
    {
        // Arrange
        var deepLink = "famick://setup?url=https%3A%2F%2Fhome.example.com&name=Home%20Server";

        // Act
        var (serverUrl, serverName, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeTrue();
        serverUrl.Should().Be("https://home.example.com");
        serverName.Should().Be("Home Server");
    }

    [Fact]
    public void ParseSetupDeepLink_WithValidHttpUrl_ReturnsServerInfo()
    {
        // Arrange
        var deepLink = "famick://setup?url=http%3A%2F%2Flocalhost%3A5000&name=Dev%20Server";

        // Act
        var (serverUrl, serverName, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeTrue();
        serverUrl.Should().Be("http://localhost:5000");
        serverName.Should().Be("Dev Server");
    }

    [Fact]
    public void ParseSetupDeepLink_WithPortNumber_ParsesCorrectly()
    {
        // Arrange
        var deepLink = "famick://setup?url=https%3A%2F%2Fhome.example.com%3A8443&name=Secure%20Server";

        // Act
        var (serverUrl, serverName, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeTrue();
        serverUrl.Should().Be("https://home.example.com:8443");
    }

    [Fact]
    public void ParseSetupDeepLink_WithoutServerName_SucceedsWithNullName()
    {
        // Arrange
        var deepLink = "famick://setup?url=https%3A%2F%2Fhome.example.com";

        // Act
        var (serverUrl, serverName, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeTrue();
        serverUrl.Should().Be("https://home.example.com");
        serverName.Should().BeNull();
    }

    [Fact]
    public void ParseSetupDeepLink_TrimsTrailingSlash()
    {
        // Arrange
        var deepLink = "famick://setup?url=https%3A%2F%2Fhome.example.com%2F&name=Test";

        // Act
        var (serverUrl, serverName, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeTrue();
        serverUrl.Should().Be("https://home.example.com");
        serverUrl.Should().NotEndWith("/");
    }

    [Fact]
    public void ParseSetupDeepLink_WithSpecialCharactersInName_DecodesCorrectly()
    {
        // Arrange
        var deepLink = "famick://setup?url=https%3A%2F%2Fhome.example.com&name=John%27s%20Home%20%26%20Kitchen";

        // Act
        var (serverUrl, serverName, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeTrue();
        serverName.Should().Be("John's Home & Kitchen");
    }

    [Fact]
    public void ParseSetupDeepLink_WithIpAddress_ParsesCorrectly()
    {
        // Arrange
        var deepLink = "famick://setup?url=https%3A%2F%2F192.168.1.100%3A5001&name=Local%20Server";

        // Act
        var (serverUrl, serverName, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeTrue();
        serverUrl.Should().Be("https://192.168.1.100:5001");
    }

    [Fact]
    public void ParseSetupDeepLink_CaseInsensitiveScheme()
    {
        // Arrange
        var deepLink = "FAMICK://SETUP?url=https%3A%2F%2Fhome.example.com&name=Test";

        // Act
        var (serverUrl, serverName, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeTrue();
    }

    #endregion

    #region Invalid Deep Link Tests

    [Fact]
    public void ParseSetupDeepLink_WrongScheme_ReturnsFalse()
    {
        // Arrange
        var deepLink = "http://setup?url=https%3A%2F%2Fhome.example.com&name=Test";

        // Act
        var (_, _, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void ParseSetupDeepLink_WrongHost_ReturnsFalse()
    {
        // Arrange
        var deepLink = "famick://config?url=https%3A%2F%2Fhome.example.com&name=Test";

        // Act
        var (_, _, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void ParseSetupDeepLink_MissingUrl_ReturnsFalse()
    {
        // Arrange
        var deepLink = "famick://setup?name=Test";

        // Act
        var (_, _, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void ParseSetupDeepLink_EmptyUrl_ReturnsFalse()
    {
        // Arrange
        var deepLink = "famick://setup?url=&name=Test";

        // Act
        var (_, _, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void ParseSetupDeepLink_InvalidUrlScheme_ReturnsFalse()
    {
        // Arrange - ftp:// is not allowed
        var deepLink = "famick://setup?url=ftp%3A%2F%2Fhome.example.com&name=Test";

        // Act
        var (_, _, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void ParseSetupDeepLink_MalformedUrl_ReturnsFalse()
    {
        // Arrange
        var deepLink = "famick://setup?url=not-a-valid-url&name=Test";

        // Act
        var (_, _, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void ParseSetupDeepLink_MalformedDeepLink_ReturnsFalse()
    {
        // Arrange
        var deepLink = "this is not a uri";

        // Act
        var (_, _, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void ParseSetupDeepLink_NullInput_ReturnsFalse()
    {
        // Arrange
        string? deepLink = null;

        // Act
        var (_, _, success) = ParseSetupDeepLink(deepLink!);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void ParseSetupDeepLink_EmptyInput_ReturnsFalse()
    {
        // Arrange
        var deepLink = "";

        // Act
        var (_, _, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeFalse();
    }

    #endregion

    #region Deep Link Generation Tests

    [Fact]
    public void GenerateDeepLink_EncodesUrlCorrectly()
    {
        // Arrange
        var serverUrl = "https://home.example.com:8443";
        var serverName = "Home Server";

        // Act
        var deepLink = GenerateDeepLink(serverUrl, serverName);

        // Assert
        deepLink.Should().StartWith("famick://setup?url=");
        deepLink.Should().Contain("https%3a%2f%2fhome.example.com%3a8443");
        deepLink.Should().Contain("&name=Home+Server");
    }

    [Fact]
    public void GenerateDeepLink_EncodesSpecialCharactersInName()
    {
        // Arrange
        var serverUrl = "https://home.example.com";
        var serverName = "John's Home & Kitchen";

        // Act
        var deepLink = GenerateDeepLink(serverUrl, serverName);

        // Assert
        deepLink.Should().Contain("John%27s+Home+%26+Kitchen");
    }

    [Fact]
    public void GenerateDeepLink_RoundTripsCorrectly()
    {
        // Arrange
        var originalUrl = "https://home.example.com:8443";
        var originalName = "John's Home & Kitchen";

        // Act
        var deepLink = GenerateDeepLink(originalUrl, originalName);
        var (parsedUrl, parsedName, success) = ParseSetupDeepLink(deepLink);

        // Assert
        success.Should().BeTrue();
        parsedUrl.Should().Be(originalUrl);
        parsedName.Should().Be(originalName);
    }

    /// <summary>
    /// Simulates the deep link generation from SetupApiController.
    /// </summary>
    private string GenerateDeepLink(string serverUrl, string serverName)
    {
        var encodedUrl = HttpUtility.UrlEncode(serverUrl);
        var encodedName = HttpUtility.UrlEncode(serverName);
        return $"famick://setup?url={encodedUrl}&name={encodedName}";
    }

    #endregion
}
