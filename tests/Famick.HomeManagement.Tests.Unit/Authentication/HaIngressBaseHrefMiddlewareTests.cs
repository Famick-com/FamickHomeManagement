using System.Text;
using Famick.HomeManagement.Web.Shared.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Tests.Unit.Authentication;

public class HaIngressBaseHrefMiddlewareTests
{
    private const string Shell =
        "<!DOCTYPE html><html><head><base href=\"/\" />" +
        "</head><body><img src=\"_content/Famick.HomeManagement.UI/images/logo-lockup.svg\" /></body></html>";

    [Fact]
    public async Task Ingress_HtmlShell_RewritesBaseHrefToPrefix()
    {
        var output = await RunAsync(
            pathBase: "/api/hassio_ingress/abc",
            path: "/",
            contentType: "text/html; charset=utf-8",
            body: Shell);

        output.Should().Contain("<base href=\"/api/hassio_ingress/abc/\" />");
        output.Should().NotContain("<base href=\"/\" />");
    }

    [Fact]
    public async Task Ingress_HtmlShell_SetsContentLengthToRewrittenLength()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.PathBase = new PathString("/api/hassio_ingress/abc");
        ctx.Request.Path = "/";
        var sink = new MemoryStream();
        ctx.Response.Body = sink;

        await new HaIngressBaseHrefMiddleware(WriteHtml(Shell)).InvokeAsync(ctx);

        ctx.Response.ContentLength.Should().Be(sink.Length);
    }

    [Fact]
    public async Task NotBehindIngress_LeavesShellUntouched()
    {
        // No PathBase => normal self-hosted/cloud deployment => never rewrite.
        var output = await RunAsync(
            pathBase: "",
            path: "/",
            contentType: "text/html",
            body: Shell);

        output.Should().Be(Shell);
    }

    [Fact]
    public async Task Ingress_FrameworkAsset_PassesThroughUnbuffered()
    {
        const string js = "export const boot = 1; // <base href=\"/\" />";
        var output = await RunAsync(
            pathBase: "/api/hassio_ingress/abc",
            path: "/_framework/blazor.webassembly.js",
            contentType: "text/javascript",
            body: js);

        output.Should().Be(js);
    }

    [Fact]
    public async Task Ingress_NonHtmlBodyOnSpaPath_NotRewritten()
    {
        // Extensionless path passes the pre-filter, but a non-HTML content type
        // must not be touched (defensive: API-ish JSON served off a bare path).
        const string json = "{\"note\":\"<base href=\\\"/\\\" />\"}";
        var output = await RunAsync(
            pathBase: "/api/hassio_ingress/abc",
            path: "/data",
            contentType: "application/json",
            body: json);

        output.Should().Be(json);
    }

    private static async Task<string> RunAsync(
        string pathBase, string path, string contentType, string body)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.PathBase = new PathString(string.IsNullOrEmpty(pathBase) ? "" : pathBase);
        ctx.Request.Path = path;

        var sink = new MemoryStream();
        ctx.Response.Body = sink;

        await new HaIngressBaseHrefMiddleware(WriteBody(contentType, body)).InvokeAsync(ctx);

        sink.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(sink, Encoding.UTF8).ReadToEndAsync();
    }

    private static RequestDelegate WriteHtml(string body)
        => WriteBody("text/html; charset=utf-8", body);

    private static RequestDelegate WriteBody(string contentType, string body)
        => async context =>
        {
            context.Response.ContentType = contentType;
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(body));
        };
}
