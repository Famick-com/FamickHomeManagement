using System.Text;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Famick.HomeManagement.Web.Shared.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Http;

/// <summary>
/// Setting an ETag header is only half the fix — it has to actually make the
/// framework answer a repeat request with 304 instead of restreaming the file.
/// These drive the result through MVC's real executor to confirm it does.
/// </summary>
public class StoredFileConditionalRequestTests
{
    private const string VersionKey = "product-image-abc123.jpg";
    private static readonly byte[] FileBytes = Encoding.UTF8.GetBytes("pretend this is a JPEG");

    /// <summary>Minimal concrete controller so the protected helper can be exercised.</summary>
    private sealed class TestController : ApiControllerBase
    {
        public TestController() : base(Mock.Of<ITenantProvider>(), Mock.Of<ILogger>()) { }

        public FileStreamResult Download() =>
            StoredFile(new MemoryStream(FileBytes), "image/jpeg", VersionKey);
    }

    private static TestController CreateController(string? ifNoneMatch)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        httpContext.Request.Method = HttpMethods.Get;
        if (ifNoneMatch is not null)
        {
            httpContext.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        httpContext.Response.Body = new MemoryStream();

        return new TestController
        {
            ControllerContext = new ControllerContext(
                new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor()))
        };
    }

    private static async Task<HttpResponse> ExecuteAsync(TestController controller)
    {
        var result = controller.Download();
        await result.ExecuteResultAsync(controller.ControllerContext);
        return controller.Response;
    }

    [Fact]
    public async Task A_first_request_streams_the_file_with_cache_headers()
    {
        var response = await ExecuteAsync(CreateController(ifNoneMatch: null));

        response.StatusCode.Should().Be(StatusCodes.Status200OK);
        response.Headers.ETag.ToString().Should().NotBeEmpty();
        response.Headers.CacheControl.ToString().Should().Contain("immutable");
        response.Body.Length.Should().Be(FileBytes.Length);
    }

    [Fact]
    public async Task A_repeat_request_with_the_matching_etag_gets_304_and_no_body()
    {
        // This is the behaviour the alarm was really about: the client had no way to
        // say "I already have this", so every render restreamed all 230 images and
        // each one cost a database round trip (issue #80).
        var etag = StoredFileCache.ComputeETag(VersionKey).ToString();

        var response = await ExecuteAsync(CreateController(ifNoneMatch: etag));

        response.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        response.Body.Length.Should().Be(0);
    }

    [Fact]
    public async Task A_request_holding_a_different_version_gets_the_new_file()
    {
        var staleEtag = StoredFileCache.ComputeETag("some-older-file.jpg").ToString();

        var response = await ExecuteAsync(CreateController(ifNoneMatch: staleEtag));

        response.StatusCode.Should().Be(StatusCodes.Status200OK);
        response.Body.Length.Should().Be(FileBytes.Length);
    }
}
