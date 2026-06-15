using System.Net;
using System.Text.Encodings.Web;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Web.Shared.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Tests.Unit.Authentication;

public class HaIngressAuthenticationHandlerTests
{
    private static readonly Guid FamickUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task AuthenticateAsync_DisabledScheme_ReturnsNoResult()
    {
        var handler = await BuildHandlerAsync(
            settings: new HaIngressSettings { Enabled = false, TrustedProxies = new() { "172.30.32.0/23" } },
            remoteIp: IPAddress.Parse("172.30.32.2"),
            userIdHeader: "abc-123");

        var result = await handler.AuthenticateAsync();

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_HeaderAbsent_ReturnsNoResult()
    {
        var handler = await BuildHandlerAsync(
            settings: new HaIngressSettings { Enabled = true, TrustedProxies = new() { "172.30.32.0/23" } },
            remoteIp: IPAddress.Parse("172.30.32.2"),
            userIdHeader: null);

        var result = await handler.AuthenticateAsync();

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_UntrustedSource_Fails()
    {
        var handler = await BuildHandlerAsync(
            settings: new HaIngressSettings { Enabled = true, TrustedProxies = new() { "172.30.32.0/23" } },
            remoteIp: IPAddress.Parse("8.8.8.8"),
            userIdHeader: "abc-123");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_EmptyTrustedProxies_FailsEvenForLoopback()
    {
        var handler = await BuildHandlerAsync(
            settings: new HaIngressSettings { Enabled = true, TrustedProxies = new() },
            remoteIp: IPAddress.Loopback,
            userIdHeader: "abc-123");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticateAsync_HappyPath_IssuesPrincipalWithFamickUserSub()
    {
        var handler = await BuildHandlerAsync(
            settings: new HaIngressSettings { Enabled = true, TrustedProxies = new() { "172.30.32.0/23" } },
            remoteIp: IPAddress.Parse("172.30.32.5"),
            userIdHeader: "abc-123",
            displayNameHeader: "Alice");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.FindFirst("sub")!.Value.Should().Be(FamickUserId.ToString());
        result.Principal.FindFirst("name")!.Value.Should().Be("Alice");
    }

    private static async Task<HaIngressAuthenticationHandler> BuildHandlerAsync(
        HaIngressSettings settings,
        IPAddress remoteIp,
        string? userIdHeader,
        string? displayNameHeader = null)
    {
        var resolver = new StubResolver(new User { Id = FamickUserId });
        var handler = new HaIngressAuthenticationHandler(
            new StaticOptionsMonitor<HaIngressAuthenticationOptions>(new HaIngressAuthenticationOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            new StaticOptionsMonitor<HaIngressSettings>(settings),
            resolver);

        var scheme = new AuthenticationScheme(
            HaIngressAuthenticationDefaults.AuthenticationScheme,
            displayName: null,
            handlerType: typeof(HaIngressAuthenticationHandler));

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;
        if (userIdHeader is not null)
        {
            httpContext.Request.Headers[HaIngressAuthenticationDefaults.UserIdHeader] = userIdHeader;
        }
        if (displayNameHeader is not null)
        {
            httpContext.Request.Headers[HaIngressAuthenticationDefaults.UserDisplayNameHeader] = displayNameHeader;
        }

        await handler.InitializeAsync(scheme, httpContext);
        return handler;
    }

    private sealed class StubResolver : IHaIngressUserResolver
    {
        private readonly User _user;
        public StubResolver(User user) { _user = user; }
        public Task<User> ResolveAsync(HaIngressIdentity identity, CancellationToken cancellationToken = default)
            => Task.FromResult(_user);
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) { CurrentValue = value; }
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();
        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
    }
}
