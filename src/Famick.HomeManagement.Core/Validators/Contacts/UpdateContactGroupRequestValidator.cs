using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Domain.Enums;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Contacts;

public class UpdateContactGroupRequestValidator : AbstractValidator<UpdateContactGroupRequest>
{
    public UpdateContactGroupRequestValidator()
    {
        RuleFor(x => x.GroupName)
            .NotEmpty().WithMessage("Group name is required")
            .MaximumLength(200).WithMessage("Group name cannot exceed 200 characters");

        RuleFor(x => x.ContactType)
            .IsInEnum().WithMessage("Contact type must be Household or Business");

        RuleFor(x => x.Website)
            .MaximumLength(500).WithMessage("Website cannot exceed 500 characters")
            .Must(BeAValidUrl).WithMessage("Website must be a valid URL")
            .When(x => !string.IsNullOrEmpty(x.Website));

        RuleFor(x => x.BusinessCategory)
            .MaximumLength(100).WithMessage("Business category cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.BusinessCategory));

        RuleFor(x => x.Website)
            .Empty().WithMessage("Website is only valid for Business groups")
            .When(x => x.ContactType == ContactType.Household && !string.IsNullOrEmpty(x.Website));

        RuleFor(x => x.BusinessCategory)
            .Empty().WithMessage("Business category is only valid for Business groups")
            .When(x => x.ContactType == ContactType.Household && !string.IsNullOrEmpty(x.BusinessCategory));

        RuleFor(x => x.Notes)
            .MaximumLength(5000).WithMessage("Notes cannot exceed 5000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }

    private static bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
