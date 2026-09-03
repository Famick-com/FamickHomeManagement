using Famick.HomeManagement.Core.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Web.Shared.Filters;

/// <summary>
/// Short-circuits the action with 404 when the server is running multi-tenant.
/// Apply to endpoints that read or write server-process state — the plugin
/// registry, the server-config overlay, mobile-app setup, remote-access
/// pairing. On a single-tenant install the admin owns the process, so those
/// are legitimately theirs to change; on a multi-tenant one they are shared by
/// every account, so an account admin must not reach them at all.
/// </summary>
/// <remarks>
/// 404 rather than 403: the endpoint does not exist on this platform, and the
/// UI already hides the corresponding sections, so a caller reaching here is
/// bypassing the UI.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SingleTenantOnlyAttribute : Attribute, IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var platformInfo = context.HttpContext.RequestServices.GetService<IPlatformInfo>();
        if (platformInfo is { IsCloud: true })
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
