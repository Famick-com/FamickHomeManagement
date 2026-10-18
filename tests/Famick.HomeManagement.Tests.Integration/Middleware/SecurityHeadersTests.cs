using System.Net.Http.Headers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Famick.HomeManagement.Web.Shared.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Tests.Integration.Middleware;

/// <summary>
/// The hardening headers every response carries, and how they combine with a policy an endpoint
/// sets for itself: a stored file's sandbox, or a server-rendered page's own policy.
/// </summary>
public class SecurityHeadersTests
{
    [Fact]
    public async Task The_spa_shell_gets_the_headers_and_a_report_only_policy()
    {
        using var host = await StartAsync(FrameAncestors.None);

        var response = await host.GetTestClient().GetAsync("/");

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        Header(response, "Content-Security-Policy").Should().Be("frame-ancestors 'none'");
        Header(response, "Content-Security-Policy-Report-Only")
            .Should().Be(SecurityHeadersMiddleware.SpaReportOnlyPolicy);
    }

    [Fact]
    public async Task An_api_response_gets_the_headers_without_a_page_policy()
    {
        using var host = await StartAsync(FrameAncestors.None);

        var response = await host.GetTestClient().GetAsync("/api/thing");

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        Header(response, "Content-Security-Policy").Should().Be("frame-ancestors 'none'");
        response.Headers.Contains("Content-Security-Policy-Report-Only").Should().BeFalse();
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    public async Task A_stored_file_is_sandboxed(string contentType)
    {
        using var host = await StartAsync(FrameAncestors.None);

        var response = await host.GetTestClient().GetAsync($"/files?type={Uri.EscapeDataString(contentType)}");

        Header(response, "Content-Security-Policy").Should().Be("sandbox; frame-ancestors 'self'");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("SAMEORIGIN");
        response.Headers.Contains("Content-Security-Policy-Report-Only").Should().BeFalse(
            "a stored HTML file is not the SPA shell");
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    [Fact]
    public async Task A_stored_pdf_is_left_viewable_and_the_app_can_preview_it_in_a_frame()
    {
        using var host = await StartAsync(FrameAncestors.None);

        var response = await host.GetTestClient().GetAsync("/files?type=application%2Fpdf");

        Header(response, "Content-Security-Policy").Should().Be("frame-ancestors 'self'");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("SAMEORIGIN");
    }

    [Fact]
    public async Task A_page_with_its_own_policy_keeps_it_and_gains_the_frame_rule()
    {
        using var host = await StartAsync(FrameAncestors.None);

        var response = await host.GetTestClient().GetAsync("/strict-page");

        Header(response, "Content-Security-Policy").Should().Be("script-src 'self'; frame-ancestors 'none'");
        response.Headers.Contains("Content-Security-Policy-Report-Only").Should().BeFalse();
    }

    [Fact]
    public async Task Self_hosted_allows_framing_by_its_own_origin()
    {
        using var host = await StartAsync(FrameAncestors.SameOrigin);

        var response = await host.GetTestClient().GetAsync("/");

        response.Headers.GetValues("X-Frame-Options").Should().Equal("SAMEORIGIN");
        Header(response, "Content-Security-Policy").Should().Be("frame-ancestors 'self'");
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? string.Join(", ", values)
            : string.Join(", ", response.Content.Headers.GetValues(name));

    private static async Task<IHost> StartAsync(FrameAncestors frameAncestors) =>
        await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(Mock.Of<ITenantProvider>());
                    services.AddControllers().ConfigureApplicationPartManager(parts =>
                    {
                        parts.ApplicationParts.Clear();
                        parts.ApplicationParts.Add(new AssemblyPart(typeof(StoredFilesController).Assembly));
                    });
                })
                .Configure(app =>
                {
                    app.UseSecurityHeaders(frameAncestors);
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/", () => Results.Content("<!doctype html><title>app</title>", "text/html"));
                        endpoints.MapGet("/api/thing", () => Results.Json(new { ok = true }));
                        endpoints.MapGet("/strict-page", (HttpContext context) =>
                        {
                            context.Response.Headers.ContentSecurityPolicy = "script-src 'self';";
                            return Results.Content("<!doctype html><title>page</title>", "text/html");
                        });
                        endpoints.MapControllers();
                    });
                }))
            .StartAsync();
}

/// <summary>Serves a body through <see cref="ApiControllerBase"/>'s stored-file helper.</summary>
[Route("files")]
public class StoredFilesController() : ApiControllerBase(Mock.Of<ITenantProvider>(), NullLogger.Instance)
{
    [HttpGet]
    public IActionResult Get(string type) =>
        StoredFile(new MemoryStream("<svg/>"u8.ToArray()), type, "stored-file-name");
}
