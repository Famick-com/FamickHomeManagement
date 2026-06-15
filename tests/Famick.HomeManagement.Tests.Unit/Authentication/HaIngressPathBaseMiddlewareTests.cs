using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Web.Shared.Authentication;
using Famick.HomeManagement.Web.Shared.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Tests.Unit.Authentication;

public class HaIngressPathBaseMiddlewareTests
{
    [Fact]
    public async Task Disabled_LeavesPathBaseAlone_EvenIfHeaderPresent()
    {
        var ctx = NewContext(headerValue: "/api/hassio_ingress/abc");
        await Build(enabled: false).InvokeAsync(ctx);

        ctx.Request.PathBase.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Enabled_NoHeader_LeavesPathBaseAlone()
    {
        var ctx = NewContext(headerValue: null);
        await Build(enabled: true).InvokeAsync(ctx);

        ctx.Request.PathBase.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Enabled_HeaderSetsPathBase()
    {
        var ctx = NewContext(headerValue: "/api/hassio_ingress/abc");
        await Build(enabled: true).InvokeAsync(ctx);

        ctx.Request.PathBase.Value.Should().Be("/api/hassio_ingress/abc");
    }

    [Fact]
    public async Task Enabled_StripsTrailingSlash()
    {
        var ctx = NewContext(headerValue: "/api/hassio_ingress/abc/");
        await Build(enabled: true).InvokeAsync(ctx);

        ctx.Request.PathBase.Value.Should().Be("/api/hassio_ingress/abc");
    }

    [Fact]
    public async Task Enabled_PrependsLeadingSlashIfMissing()
    {
        var ctx = NewContext(headerValue: "api/hassio_ingress/abc");
        await Build(enabled: true).InvokeAsync(ctx);

        ctx.Request.PathBase.Value.Should().Be("/api/hassio_ingress/abc");
    }

    [Fact]
    public async Task Enabled_BlankHeader_LeavesPathBaseAlone()
    {
        var ctx = NewContext(headerValue: "   ");
        await Build(enabled: true).InvokeAsync(ctx);

        ctx.Request.PathBase.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotMutateRequestPath()
    {
        // Routing still has to match against the un-prefixed path Supervisor
        // forwarded, so the middleware must leave Path alone.
        var ctx = NewContext(headerValue: "/api/hassio_ingress/abc");
        ctx.Request.Path = "/api/v1/todos";

        await Build(enabled: true).InvokeAsync(ctx);

        ctx.Request.Path.Value.Should().Be("/api/v1/todos");
    }

    private static HttpContext NewContext(string? headerValue)
    {
        var ctx = new DefaultHttpContext();
        if (headerValue is not null)
        {
            ctx.Request.Headers[HaIngressAuthenticationDefaults.IngressPathHeader] = headerValue;
        }
        return ctx;
    }

    private static HaIngressPathBaseMiddleware Build(bool enabled)
        => new(
            next: _ => Task.CompletedTask,
            settings: new StaticOptionsMonitor(new HaIngressSettings { Enabled = enabled }));

    private sealed class StaticOptionsMonitor : IOptionsMonitor<HaIngressSettings>
    {
        public StaticOptionsMonitor(HaIngressSettings value) { CurrentValue = value; }
        public HaIngressSettings CurrentValue { get; }
        public HaIngressSettings Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<HaIngressSettings, string?> listener) => new NoopDisposable();
        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
    }
}
