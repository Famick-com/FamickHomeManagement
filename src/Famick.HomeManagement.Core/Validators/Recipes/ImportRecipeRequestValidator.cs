using Famick.HomeManagement.Core.DTOs.Recipes;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Recipes;

public class ImportRecipeRequestValidator : AbstractValidator<ImportRecipeRequest>
{
    public ImportRecipeRequestValidator()
    {
        RuleFor(x => x.Url)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("URL is required")
            .MaximumLength(2000).WithMessage("URL cannot exceed 2000 characters")
            .Must(BeAWebUrl).WithMessage("Enter the web address of a recipe page");
    }

    private static bool BeAWebUrl(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);
}
