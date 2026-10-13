using System.Reflection;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using Famick.HomeManagement.Web.Shared.Filters;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Filters;

/// <summary>
/// Issue #110: admin endpoints that act on the whole server process, not on the
/// caller's household, must be <see cref="SingleTenantOnlyAttribute"/> so a cloud
/// account admin gets a 404. The filter's own behaviour is covered in
/// <see cref="SingleTenantOnlyAttributeTests"/>; this pins it to each endpoint.
/// </summary>
public class ProcessWideAdminEndpointsTests
{
    [Theory]
    [InlineData(typeof(ProductLookupController), nameof(ProductLookupController.ReloadPlugins))]
    [InlineData(typeof(AdminAddressController), nameof(AdminAddressController.Rehash))]
    [InlineData(typeof(AdminPluginsController), null)]
    [InlineData(typeof(AdminServerSettingsController), null)]
    public void Is_unreachable_on_the_cloud(Type controller, string? action)
    {
        var onClass = controller.GetCustomAttribute<SingleTenantOnlyAttribute>() is not null;
        var onAction = action is not null
            && controller.GetMethod(action)!.GetCustomAttribute<SingleTenantOnlyAttribute>() is not null;

        (onClass || onAction).Should().BeTrue(
            $"{controller.Name}{(action is null ? "" : "." + action)} changes state every household shares");
    }
}
