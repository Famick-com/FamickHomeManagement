using System.Reflection;
using Famick.HomeManagement.Web.Shared.Authorization;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Making and revoking a recipe's share link is an editor's everyday action, so it doesn't ask
/// for a recent sign-in.
/// </summary>
public class RecipeShareStepUpTests
{
    [Theory]
    [InlineData(nameof(RecipesController.GenerateShareToken))]
    [InlineData(nameof(RecipesController.RevokeShareToken))]
    public void Recipe_share_links_need_an_editor_but_no_recent_sign_in(string action)
    {
        var method = typeof(RecipesController).GetMethod(action)!;

        method.GetCustomAttribute<StepUpAttribute>().Should().BeNull();
        method.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("RequireEditor");
    }
}
