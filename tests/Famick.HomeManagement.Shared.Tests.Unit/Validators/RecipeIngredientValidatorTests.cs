using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Validators.Recipes;
using FluentValidation.TestHelper;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators;

/// <summary>
/// An ingredient line is a product with an amount, or text that isn't linked to a product yet.
/// </summary>
public class RecipeIngredientValidatorTests
{
    private readonly CreateRecipeIngredientRequestValidator _create = new();
    private readonly UpdateRecipeIngredientRequestValidator _update = new();

    [Fact]
    public void A_linked_line_with_an_amount_is_valid()
    {
        _create.TestValidate(new CreateRecipeIngredientRequest { ProductId = Guid.NewGuid(), Amount = 2 })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void A_linked_line_needs_an_amount()
    {
        _create.TestValidate(new CreateRecipeIngredientRequest { ProductId = Guid.NewGuid(), Amount = 0 })
            .ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void An_unlinked_line_with_text_and_no_amount_is_valid()
    {
        _create.TestValidate(new CreateRecipeIngredientRequest { OriginalText = "salt to taste" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void A_line_needs_a_product_or_text(string? text)
    {
        _create.TestValidate(new CreateRecipeIngredientRequest { OriginalText = text, Amount = 1 })
            .ShouldHaveValidationErrorFor(x => x.ProductId);
    }

    [Fact]
    public void An_empty_product_id_is_not_a_product()
    {
        _create.TestValidate(new CreateRecipeIngredientRequest { ProductId = Guid.Empty, OriginalText = "flour", Amount = 1 })
            .ShouldHaveValidationErrorFor(x => x.ProductId);
    }

    [Fact]
    public void Overlong_text_fails()
    {
        _create.TestValidate(new CreateRecipeIngredientRequest { OriginalText = new string('a', 501) })
            .ShouldHaveValidationErrorFor(x => x.OriginalText);
    }

    [Fact]
    public void Linking_on_update_needs_an_amount()
    {
        _update.TestValidate(new UpdateRecipeIngredientRequest { ProductId = Guid.NewGuid(), Amount = 0 })
            .ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Updating_an_unlinked_line_without_an_amount_is_valid()
    {
        _update.TestValidate(new UpdateRecipeIngredientRequest { Note = "optional" })
            .ShouldNotHaveAnyValidationErrors();
    }
}
