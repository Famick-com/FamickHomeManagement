using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Unit tests for Quick Consume deep link parsing and handling.
/// Tests the deep link parsing logic used by App.HandleDeepLink().
/// </summary>
public class QuickConsumeDeepLinkTests
{
    #region Quick Consume Deep Link Tests

    [Fact]
    public void ParseQuickConsumeDeepLink_WithValidUri_ReturnsTrue()
    {
        // Arrange
        var uri = new Uri("famick://quick-consume");

        // Act
        var isQuickConsume = IsQuickConsumeDeepLink(uri);

        // Assert
        isQuickConsume.Should().BeTrue();
    }

    [Fact]
    public void ParseQuickConsumeDeepLink_WithPathVariant_ReturnsTrue()
    {
        // Arrange
        var uri = new Uri("famick://app/quick-consume");

        // Act
        var isQuickConsume = IsQuickConsumeDeepLink(uri);

        // Assert
        isQuickConsume.Should().BeTrue();
    }

    [Fact]
    public void ParseQuickConsumeDeepLink_WithQueryParams_ReturnsTrue()
    {
        // Arrange
        var uri = new Uri("famick://quick-consume?source=widget");

        // Act
        var isQuickConsume = IsQuickConsumeDeepLink(uri);

        // Assert
        isQuickConsume.Should().BeTrue();
    }

    [Fact]
    public void ParseQuickConsumeDeepLink_WithDifferentHost_ReturnsFalse()
    {
        // Arrange
        var uri = new Uri("famick://shopping");

        // Act
        var isQuickConsume = IsQuickConsumeDeepLink(uri);

        // Assert
        isQuickConsume.Should().BeFalse();
    }

    [Fact]
    public void ParseQuickConsumeDeepLink_WithDifferentScheme_ReturnsFalse()
    {
        // Arrange
        var uri = new Uri("https://quick-consume");

        // Act
        var isQuickConsume = IsQuickConsumeDeepLink(uri);

        // Assert
        isQuickConsume.Should().BeFalse();
    }

    [Fact]
    public void ParseQuickConsumeDeepLink_WithVerifyHost_ReturnsFalse()
    {
        // Arrange
        var uri = new Uri("famick://verify?token=abc123");

        // Act
        var isQuickConsume = IsQuickConsumeDeepLink(uri);

        // Assert
        isQuickConsume.Should().BeFalse();
    }

    [Fact]
    public void ParseQuickConsumeDeepLink_WithSetupHost_ReturnsFalse()
    {
        // Arrange
        var uri = new Uri("famick://setup?url=https://example.com");

        // Act
        var isQuickConsume = IsQuickConsumeDeepLink(uri);

        // Assert
        isQuickConsume.Should().BeFalse();
    }

    #endregion

    #region Deep Link Priority Tests

    [Fact]
    public void DeepLinkPriority_QuickConsumeShouldBeHandledBeforeShopping()
    {
        // This test documents the expected priority order in App.HandleDeepLink()
        // Priority: 1. Setup, 2. Quick Consume, 3. Verify, 4. Shopping

        var quickConsumeUri = new Uri("famick://quick-consume");
        var shoppingUri = new Uri("famickshopping://shopping/session?ListId=123");

        // Both should be recognized as valid deep links
        IsQuickConsumeDeepLink(quickConsumeUri).Should().BeTrue();
        IsShoppingDeepLink(shoppingUri).Should().BeTrue();

        // Quick consume should NOT match shopping pattern
        IsShoppingDeepLink(quickConsumeUri).Should().BeFalse();
    }

    #endregion

    #region URL Scheme Registration Tests

    [Theory]
    [InlineData("famick://quick-consume")]
    [InlineData("famick://setup")]
    [InlineData("famick://verify")]
    public void FamickScheme_ShouldMatchRegisteredUrls(string urlString)
    {
        // Arrange
        var uri = new Uri(urlString);

        // Act & Assert
        uri.Scheme.Should().Be("famick");
    }

    [Theory]
    [InlineData("famickshopping://shopping/session")]
    public void FamickShoppingScheme_ShouldMatchRegisteredUrls(string urlString)
    {
        // Arrange
        var uri = new Uri(urlString);

        // Act & Assert
        uri.Scheme.Should().Be("famickshopping");
    }

    #endregion

    #region Query String Parsing Tests

    [Fact]
    public void ParseQueryString_WithEncodedValues_DecodesCorrectly()
    {
        // Arrange
        var uri = new Uri("famick://setup?url=https%3A%2F%2Fexample.com&name=Home%20Server");

        // Act
        var query = ParseQueryString(uri.Query);

        // Assert
        query["url"].Should().Be("https://example.com");
        query["name"].Should().Be("Home Server");
    }

    [Fact]
    public void ParseQueryString_WithPlusAsSpace_DecodesCorrectly()
    {
        // Arrange
        var uri = new Uri("famick://setup?name=Home+Server");

        // Act
        var query = ParseQueryString(uri.Query);

        // Assert
        query["name"].Should().Be("Home Server");
    }

    [Fact]
    public void ParseQueryString_WithEmptyQuery_ReturnsEmptyDictionary()
    {
        // Arrange
        var uri = new Uri("famick://quick-consume");

        // Act
        var query = ParseQueryString(uri.Query);

        // Assert
        query.Should().BeEmpty();
    }

    [Fact]
    public void ParseQueryString_IsCaseInsensitive()
    {
        // Arrange
        var uri = new Uri("famick://setup?URL=https://example.com&Name=Test");

        // Act
        var query = ParseQueryString(uri.Query);

        // Assert
        query.ContainsKey("url").Should().BeTrue();
        query.ContainsKey("name").Should().BeTrue();
    }

    #endregion

    #region Helper Methods (Simulating App.xaml.cs logic)

    /// <summary>
    /// Simulates the quick-consume deep link detection logic from App.HandleDeepLink()
    /// </summary>
    private static bool IsQuickConsumeDeepLink(Uri uri)
    {
        if (uri.Scheme != "famick")
            return false;

        return uri.Host == "quick-consume" || uri.AbsolutePath.Contains("quick-consume");
    }

    /// <summary>
    /// Simulates the shopping deep link detection logic
    /// </summary>
    private static bool IsShoppingDeepLink(Uri uri)
    {
        if (uri.Scheme != "famickshopping")
            return false;

        var query = ParseQueryString(uri.Query);
        return query.ContainsKey("ListId");
    }

    /// <summary>
    /// Simulates the query string parsing from App.xaml.cs
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(query))
            return result;

        if (query.StartsWith("?"))
            query = query[1..];

        foreach (var pair in query.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                // Replace + with space before URL decoding (standard form encoding)
                var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
                var value = Uri.UnescapeDataString(parts[1].Replace('+', ' '));
                result[key] = value;
            }
        }

        return result;
    }

    #endregion
}
