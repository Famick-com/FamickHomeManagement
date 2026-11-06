using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Validators.Recipes;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators;

/// <summary>
/// An import needs the web address of a page: a missing one is a validation error, not a refused page.
/// </summary>
public class ImportRecipeRequestValidatorTests
{
    private readonly ImportRecipeRequestValidator _validator = new();

    [Theory]
    [InlineData("https://recipes.example/pancakes/")]
    [InlineData("http://192.168.1.20:8080/recipe")]
    public void A_web_address_is_valid(string url)
    {
        _validator.TestValidate(new ImportRecipeRequest { Url = url }).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void A_missing_address_is_required(string? url)
    {
        _validator.TestValidate(new ImportRecipeRequest { Url = url! })
            .ShouldHaveValidationErrorFor(x => x.Url).WithErrorMessage("URL is required");
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://files.example/recipe")]
    [InlineData("/recipes/pancakes")]
    public void Anything_but_an_http_address_is_refused(string url)
    {
        _validator.TestValidate(new ImportRecipeRequest { Url = url })
            .ShouldHaveValidationErrorFor(x => x.Url).WithErrorMessage("Enter the web address of a recipe page");
    }

    [Fact]
    public void An_overlong_address_is_refused_once()
    {
        var result = _validator.TestValidate(new ImportRecipeRequest { Url = "https://recipes.example/" + new string('a', 2000) });

        result.ShouldHaveValidationErrorFor(x => x.Url).WithErrorMessage("URL cannot exceed 2000 characters");
        result.Errors.Should().ContainSingle();
    }
}
