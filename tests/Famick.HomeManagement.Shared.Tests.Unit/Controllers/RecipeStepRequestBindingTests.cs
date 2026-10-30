using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Recipes;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// [ApiController] validates the bound request before the action runs, and treats a
/// non-nullable property as required. A step sent with "instructions": null has to get past
/// that check to reach the FluentValidation rules, which allow it.
/// </summary>
public class RecipeStepRequestBindingTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("""{"instructions":null}""")]
    [InlineData("""{"instructions":""}""")]
    [InlineData("""{"title":"Gather"}""")]
    public void Create_request_without_instructions_passes_model_validation(string json)
    {
        var request = JsonSerializer.Deserialize<CreateRecipeStepRequest>(json, Web)!;

        request.Instructions.Should().BeEmpty();
        ModelStateFor(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("""{"instructions":null}""")]
    [InlineData("""{"instructions":""}""")]
    [InlineData("""{"title":"Gather"}""")]
    public void Update_request_without_instructions_passes_model_validation(string json)
    {
        var request = JsonSerializer.Deserialize<UpdateRecipeStepRequest>(json, Web)!;

        request.Instructions.Should().BeEmpty();
        ModelStateFor(request).IsValid.Should().BeTrue();
    }

    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ModelStateFor(object model)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddControllers();
        var provider = services.BuildServiceProvider();
        var context = new ActionContext(
            new DefaultHttpContext { RequestServices = provider }, new RouteData(), new ActionDescriptor());
        provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, string.Empty, model);
        return context.ModelState;
    }
}
