using System.Reflection;
using System.Security.Claims;
using Famick.HomeManagement.Web.Shared;
using Famick.HomeManagement.Web.Shared.Authorization;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Shared.Tests.Unit.Authorization;

/// <summary>
/// Every state-changing action says which household roles may call it.
/// </summary>
/// <remarks>
/// Bare <c>[Authorize]</c> admits anyone signed in to the household, Viewers included. That is
/// right for reads and wrong for almost every write, and nothing at runtime notices the
/// difference — a write that forgets its policy simply works for everyone. This test makes the
/// omission a build failure: a new POST, PUT, PATCH or DELETE has to name a role policy, allow
/// anonymous access, or carry <see cref="NoRolePolicyAttribute"/> with a reason.
/// </remarks>
public class ControllerRolePolicyTests
{
    private static readonly string[] ReadOnlyVerbs = ["GET", "HEAD", "OPTIONS"];

    public static IEnumerable<MethodInfo> WriteActions() =>
        typeof(AuthorizationPolicies).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                .SelectMany(a => a.HttpMethods)
                .Any(verb => !ReadOnlyVerbs.Contains(verb, StringComparer.OrdinalIgnoreCase)));

    private static bool DeclaresRoles(MemberInfo member) =>
        member.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Any(a => !string.IsNullOrEmpty(a.Policy) || !string.IsNullOrEmpty(a.Roles))
        || member.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);

    [Fact]
    public void EveryWriteActionNamesARolePolicyOrOptsOut()
    {
        var unguarded = WriteActions()
            .Where(m => !DeclaresRoles(m)
                        && !DeclaresRoles(m.DeclaringType!)
                        && !m.IsDefined(typeof(NoRolePolicyAttribute)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        WriteActions().Should().NotBeEmpty("the scan has to find the controllers to mean anything");
        unguarded.Should().BeEmpty(
            "bare [Authorize] lets a Viewer write. Add [Authorize(Policy = \"RequireEditor\")] (or " +
            "another role policy), or [NoRolePolicy(\"why\")] if the action only touches the " +
            "caller's own data or does not write");
    }

    [Fact]
    public void OptOutsGiveAReason()
    {
        WriteActions()
            .Select(m => m.GetCustomAttribute<NoRolePolicyAttribute>())
            .OfType<NoRolePolicyAttribute>()
            .Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a.Reason));
    }

    public static TheoryData<Type, string> HouseholdWrites() => new()
    {
        { typeof(MealsController), nameof(MealsController.Create) },
        { typeof(MealsController), nameof(MealsController.Update) },
        { typeof(MealsController), nameof(MealsController.Delete) },
        { typeof(MealTypesController), nameof(MealTypesController.Create) },
        { typeof(MealTypesController), nameof(MealTypesController.Update) },
        { typeof(MealTypesController), nameof(MealTypesController.Delete) },
        { typeof(MealPlansController), nameof(MealPlansController.AddEntry) },
        { typeof(MealPlansController), nameof(MealPlansController.UpdateEntry) },
        { typeof(MealPlansController), nameof(MealPlansController.DeleteEntry) },
        { typeof(MealPlansController), nameof(MealPlansController.Delete) },
        { typeof(MealPlansController), nameof(MealPlansController.GenerateShoppingList) },
        { typeof(WizardController), nameof(WizardController.SaveHouseholdInfo) },
        { typeof(WizardController), nameof(WizardController.SaveHomeStatistics) },
        { typeof(WizardController), nameof(WizardController.SaveMaintenanceItems) },
        { typeof(ProductsController), nameof(ProductsController.AddImageFromUrl) },
    };

    [Theory]
    [MemberData(nameof(HouseholdWrites))]
    public async Task HouseholdWrites_RefuseAViewerAndAdmitAnEditor(Type controller, string action)
    {
        var policy = controller.GetMethod(action)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .Single(p => !string.IsNullOrEmpty(p))!;

        var authorization = new ServiceCollection()
            .AddLogging()
            .AddAuthorization(AuthorizationPolicies.Configure)
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

        (await authorization.AuthorizeAsync(Member("Viewer"), policy)).Succeeded
            .Should().BeFalse($"a Viewer must get 403 from {controller.Name}.{action}");
        (await authorization.AuthorizeAsync(Member("Editor"), policy)).Succeeded
            .Should().BeTrue($"an Editor must still be able to call {controller.Name}.{action}");
    }

    private static ClaimsPrincipal Member(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));
}
