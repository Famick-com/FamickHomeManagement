using Famick.HomeManagement.Core.DTOs.Contacts;
using FluentValidation;

namespace Famick.HomeManagement.Core.Validators.Contacts;

public class AddRelationshipRequestValidator : AbstractValidator<AddRelationshipRequest>
{
    public AddRelationshipRequestValidator()
    {
        RuleFor(x => x.TargetContactId)
            .NotEmpty().WithMessage("Target contact is required");

        RuleFor(x => x.RelationshipType)
            .IsInEnum().WithMessage("Invalid relationship type");

        RuleFor(x => x.CustomLabel)
            .MaximumLength(100).WithMessage("Custom label cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.CustomLabel));
    }
}
