using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Phase 5 chunk 5.K — <see cref="AuthHostRoutingHandler"/> rewrites the host
/// of outgoing auth requests to <c>auth.famick.com</c> when the persisted
/// <c>use_auth_famick_com</c> flag is on; everything else passes through
/// unchanged. Symmetric to the mobile <c>DynamicApiHttpHandler</c>'s
/// <c>IsAuthPath</c> decision from chunk 5.J.
/// </summary>
public class AuthHostRoutingHandlerTests
{
    private const string AppHost = "https://app.famick.com";

    private static (HttpClient client, CapturingHandler capture) BuildClient(bool flagOn)
    {
        var storage = new Mock<IAuthHostFlagStorage>();
        storage.Setup(s => s.GetUseAuthFamickComAsync()).ReturnsAsync(flagOn);

        var capture = new CapturingHandler();
        var routing = new AuthHostRoutingHandler(storage.Object) { InnerHandler = capture };
        var client = new HttpClient(routing) { BaseAddress = new Uri(AppHost) };
        return (client, capture);
    }

    [Theory]
    [InlineData("api/auth/login")]
    [InlineData("api/auth/refresh")]
    [InlineData("api/auth/external/config")]
    [InlineData("api/auth/external/google/challenge")]
    [InlineData("api/auth/passkey/authenticate/verify")]
    [InlineData("api/auth/reauth")]
    [InlineData("check")]
    [InlineData(".well-known/jwks.json")]
    public async Task Auth_paths_rewrite_to_auth_host_when_flag_on(string path)
    {
        var (client, capture) = BuildClient(flagOn: true);

        await client.GetAsync(path);

        capture.LastRequestUri.Should().NotBeNull();
        capture.LastRequestUri!.Host.Should().Be("auth.famick.com");
        capture.LastRequestUri.AbsolutePath.Should().Be("/" + path);
    }

    [Theory]
    [InlineData("api/auth/login")]
    [InlineData("check")]
    public async Task Auth_paths_unchanged_when_flag_off(string path)
    {
        var (client, capture) = BuildClient(flagOn: false);

        await client.GetAsync(path);

        capture.LastRequestUri!.Host.Should().Be("app.famick.com");
    }

    [Theory]
    [InlineData("api/v1/products")]
    [InlineData("api/v1/wizard/members/check-duplicate")]
    [InlineData("api/setup/status")]
    public async Task Non_auth_paths_unchanged_regardless_of_flag(string path)
    {
        var (client, capture) = BuildClient(flagOn: true);

        await client.GetAsync(path);

        capture.LastRequestUri!.Host.Should().Be("app.famick.com");
    }

    [Fact]
    public async Task Query_string_preserved_on_rewrite()
    {
        var (client, capture) = BuildClient(flagOn: true);

        await client.GetAsync("api/auth/external/google/challenge?callbackUrl=foo&extra=bar");

        capture.LastRequestUri!.Host.Should().Be("auth.famick.com");
        capture.LastRequestUri.Query.Should().Contain("callbackUrl=foo").And.Contain("extra=bar");
    }

    [Fact]
    public async Task Path_matching_is_case_insensitive()
    {
        var (client, capture) = BuildClient(flagOn: true);

        await client.GetAsync("API/AUTH/Login");

        capture.LastRequestUri!.Host.Should().Be("auth.famick.com");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
