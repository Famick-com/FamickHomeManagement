using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Validators.Recipes;
using FluentValidation.TestHelper;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators;

/// <summary>
/// Step instructions are optional: a step can be only a title and ingredients, and the clients
/// save a step before the cook has written anything in it.
/// </summary>
public class RecipeStepValidatorTests
{
    private readonly CreateRecipeStepRequestValidator _create = new();
    private readonly UpdateRecipeStepRequestValidator _update = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_without_instructions_is_valid(string? instructions)
    {
        _create.TestValidate(new CreateRecipeStepRequest { Instructions = instructions! })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Update_without_instructions_is_valid(string? instructions)
    {
        _update.TestValidate(new UpdateRecipeStepRequest { Instructions = instructions! })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_with_overlong_instructions_fails()
    {
        _create.TestValidate(new CreateRecipeStepRequest { Instructions = new string('a', 10001) })
            .ShouldHaveValidationErrorFor(x => x.Instructions);
    }

    [Fact]
    public void Update_with_overlong_instructions_fails()
    {
        _update.TestValidate(new UpdateRecipeStepRequest { Instructions = new string('a', 10001) })
            .ShouldHaveValidationErrorFor(x => x.Instructions);
    }

    [Fact]
    public void Instructions_at_the_limit_are_valid()
    {
        _create.TestValidate(new CreateRecipeStepRequest { Instructions = new string('a', 10000) })
            .ShouldNotHaveAnyValidationErrors();
    }
}
