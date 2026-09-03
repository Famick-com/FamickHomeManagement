using Famick.HomeManagement.Core.Platform;
using Famick.HomeManagement.Web.Shared.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Tests.Unit.Filters;

public class SingleTenantOnlyAttributeTests
{
    [Theory]
    [InlineData(ServerPlatform.SelfHosted)]
    [InlineData(ServerPlatform.HomeAssistant)]
    public void Allows_the_action_on_single_tenant_platforms(ServerPlatform platform)
    {
        var context = BuildContext(new PlatformInfo(platform));

        new SingleTenantOnlyAttribute().OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void Returns_not_found_on_a_multi_tenant_platform()
    {
        var context = BuildContext(new PlatformInfo(ServerPlatform.Cloud));

        new SingleTenantOnlyAttribute().OnActionExecuting(context);

        context.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Allows_the_action_when_no_platform_is_registered()
    {
        // Hosts that never registered IPlatformInfo are single-tenant by
        // definition, so the filter must not lock them out.
        var context = BuildContext(platformInfo: null);

        new SingleTenantOnlyAttribute().OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    private static ActionExecutingContext BuildContext(IPlatformInfo? platformInfo)
    {
        var services = new ServiceCollection();
        if (platformInfo is not null)
        {
            services.AddSingleton(platformInfo);
        }

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }
}
