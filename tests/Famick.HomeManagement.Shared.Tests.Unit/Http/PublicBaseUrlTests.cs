using Famick.HomeManagement.Web.Shared.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Shared.Tests.Unit.Http;

public class PublicBaseUrlTests
{
    private static HttpRequest SpoofedRequest(string? configuredBaseUrl)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = configuredBaseUrl })
            .Build();

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<IConfiguration>(config).BuildServiceProvider()
        };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("evil.example");
        return context.Request;
    }

    [Fact]
    public void Resolve_PrefersTheConfiguredBaseUrlOverTheRequestHost()
    {
        PublicBaseUrl.Resolve(SpoofedRequest("https://app.famick.com/")).Should().Be("https://app.famick.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithNoConfiguredBaseUrl_UsesTheRequest(string? configured)
    {
        PublicBaseUrl.Resolve(SpoofedRequest(configured)).Should().Be("https://evil.example");
    }

    [Fact]
    public void Resolve_WithNoRequestServices_UsesTheRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost:5000");

        PublicBaseUrl.Resolve(context.Request).Should().Be("http://localhost:5000");
    }
}
