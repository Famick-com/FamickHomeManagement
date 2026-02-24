using Famick.HomeManagement.Core.DTOs.Contacts;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Contacts;

public class UpdateContactRequestValidator : AbstractValidator<UpdateContactRequest>
{
    public UpdateContactRequestValidator()
    {
        // Conditional validation: CompanyName OR (FirstName OR LastName) required
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.CompanyName) ||
                       !string.IsNullOrWhiteSpace(x.FirstName) ||
                       !string.IsNullOrWhiteSpace(x.LastName))
            .WithMessage("Either company name or at least first name or last name is required");

        RuleFor(x => x.FirstName)
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.LastName));

        RuleFor(x => x.MiddleName)
            .MaximumLength(100).WithMessage("Middle name cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.MiddleName));

        RuleFor(x => x.PreferredName)
            .MaximumLength(100).WithMessage("Preferred name cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.PreferredName));

        RuleFor(x => x.CompanyName)
            .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.CompanyName));

        RuleFor(x => x.Title)
            .MaximumLength(100).WithMessage("Title cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Notes)
            .MaximumLength(5000).WithMessage("Notes cannot exceed 5000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        // Birth date validation
        RuleFor(x => x.BirthYear)
            .InclusiveBetween(1800, 2100).WithMessage("Birth year must be between 1800 and 2100")
            .When(x => x.BirthYear.HasValue);

        RuleFor(x => x.BirthMonth)
            .InclusiveBetween(1, 12).WithMessage("Birth month must be between 1 and 12")
            .When(x => x.BirthMonth.HasValue);

        RuleFor(x => x.BirthDay)
            .InclusiveBetween(1, 31).WithMessage("Birth day must be between 1 and 31")
            .When(x => x.BirthDay.HasValue);

        // Death date validation
        RuleFor(x => x.DeathYear)
            .InclusiveBetween(1800, 2100).WithMessage("Death year must be between 1800 and 2100")
            .When(x => x.DeathYear.HasValue);

        RuleFor(x => x.DeathMonth)
            .InclusiveBetween(1, 12).WithMessage("Death month must be between 1 and 12")
            .When(x => x.DeathMonth.HasValue);

        RuleFor(x => x.DeathDay)
            .InclusiveBetween(1, 31).WithMessage("Death day must be between 1 and 31")
            .When(x => x.DeathDay.HasValue);
    }
}
